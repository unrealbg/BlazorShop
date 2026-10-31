import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const web = join(repository, 'BlazorShop.Presentation', 'BlazorShop.Web');
const output = resolve(repository, process.argv[2] ?? 'artifacts/web-build-toolchain');
const require = createRequire(join(web, 'package.json'));
const postcss = require('postcss');
const tailwind = require('tailwindcss');
const { loadConfig } = require('tailwindcss/lib/lib/load-config');
const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
mkdirSync(output, { recursive: true });
const fixture = mkdtempSync(join(output, 'probe-'));

function hasRule(root, selector, property, value, media) {
    let found = false;
    root.walkRules(rule => {
        if (!rule.selectors.includes(selector)) return;
        if (media && !(rule.parent.type === 'atrule' && rule.parent.name === 'media' && rule.parent.params.replaceAll(' ', '') === media)) return;
        rule.walkDecls(property, declaration => { if (declaration.value === value) found = true; });
    });
    return found;
}

try {
    // Exercise Tailwind's real Sucrase-backed TypeScript/ES module config loader.
    const configPath = join(fixture, 'tailwind.config.ts');
    writeFileSync(configPath, `import type { Config } from 'tailwindcss';
const config: Config = {
  content: [{ raw: '<div class="hidden md:block sm:grid-cols-2 lg:grid-cols-4"></div>', extension: 'html' }],
  corePlugins: { preflight: false }
};
export default config;
`);
    const config = loadConfig(configPath);
    const probe = await postcss([tailwind(config)]).process('@tailwind utilities;', { from: undefined });
    const root = postcss.parse(probe.css);
    assert(hasRule(root, '.hidden', 'display', 'none'), 'Missing hidden utility');
    assert(hasRule(root, '.md\\:block', 'display', 'block', '(min-width:768px)'), 'Responsive visibility changed');
    assert(hasRule(root, '.sm\\:grid-cols-2', 'grid-template-columns', 'repeat(2, minmax(0, 1fr))', '(min-width:640px)'), 'Small grid breakpoint changed');
    assert(hasRule(root, '.lg\\:grid-cols-4', 'grid-template-columns', 'repeat(4, minmax(0, 1fr))', '(min-width:1024px)'), 'Large grid breakpoint changed');

    // The app uses the transform API. Also exercise the changed CLI glob path on CI's POSIX platform.
    // Sucrase 3.35.0 and 3.35.1 both fail this unused CLI path with native Windows separators.
    if (process.platform !== 'win32') {
        mkdirSync(join(fixture, 'project', 'src'), { recursive: true });
        writeFileSync(join(fixture, 'project', 'src', 'probe.ts'), 'export const answer: number = 42;');
        writeFileSync(join(fixture, 'project', 'src', 'ignored.d.ts'), 'declare const ignored: number;');
        writeFileSync(join(fixture, 'project', 'tsconfig.json'), JSON.stringify({
            compilerOptions: { outDir: 'compiled', module: 'commonjs' }, include: ['src/**/*.ts'],
        }));
        execFileSync(process.execPath, [require.resolve('sucrase/bin/sucrase'), '--project', 'project', '--quiet'], { cwd: fixture, stdio: 'pipe' });
        assert.equal(require(join(fixture, 'project', 'compiled', 'src', 'probe.js')).answer, 42, 'Sucrase CLI include-glob transform failed');
    }

    const cssPath = join(web, 'wwwroot', 'css', 'site.css');
    const css = readFileSync(cssPath);
    assert(css.length > 1000, 'Published utility stylesheet is unexpectedly small');
    const actual = postcss.parse(css.toString());
    assert(!actual.nodes.some(node => node.type === 'atrule' && node.name === 'tailwind'), 'Tailwind directives were not compiled');
    assert(hasRule(actual, '.hidden', 'display', 'none'), 'Application hidden utility missing');
    assert(hasRule(actual, '.md\\:grid-cols-2', 'grid-template-columns', 'repeat(2,minmax(0,1fr))', '(min-width:768px)'), 'Application responsive grid missing');
    const selectors = [];
    const media = [];
    actual.walkRules(rule => selectors.push(rule.selector));
    actual.walkAtRules('media', rule => media.push(rule.params));
    const report = {
        node: process.version,
        tailwind: require('tailwindcss/package.json').version,
        sucrase: require('sucrase/package.json').version,
        typescriptConfig: 'passed', cliIncludeGlob: process.platform === 'win32' ? 'not-run: pre-existing Windows CLI path limitation' : 'passed', responsiveVisibility: 'passed',
        cssBytes: css.length, cssSha256: sha256(css),
        selectorCount: selectors.length, selectorsSha256: sha256(selectors.join('\n')),
        mediaCount: media.length, mediaSha256: sha256(media.join('\n')),
    };
    copyFileSync(cssPath, join(output, 'site.css'));
    writeFileSync(join(output, 'verification.json'), JSON.stringify(report, null, 2) + '\n');
    console.log(JSON.stringify(report, null, 2));
} finally {
    if (!resolve(fixture).startsWith(output + sep + 'probe-')) throw new Error('Unexpected fixture cleanup path');
    rmSync(fixture, { recursive: true, force: true });
}
