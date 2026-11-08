"""Read a single-platform OCI archive and verify its manifest, config and source label."""

import hashlib
import json
import re
import sys
import tarfile


def read_json_blob(archive, descriptor):
    digest = descriptor["digest"]
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", digest):
        raise ValueError(f"Invalid OCI digest: {digest}")
    with archive.extractfile(f"blobs/sha256/{digest.split(':')[1]}") as blob:
        data = blob.read()
    if digest != "sha256:" + hashlib.sha256(data).hexdigest():
        raise ValueError(f"OCI blob does not match {digest}")
    return json.loads(data)


def main(path, expected_sha):
    with tarfile.open(path, "r:") as archive:
        with archive.extractfile("index.json") as index:
            (descriptor,) = json.load(index)["manifests"]
        if descriptor["mediaType"] != "application/vnd.oci.image.manifest.v1+json":
            raise ValueError("Expected a single OCI image manifest")
        manifest = read_json_blob(archive, descriptor)
        config = read_json_blob(archive, manifest["config"])

    if (config["os"], config["architecture"]) != ("linux", "amd64"):
        raise ValueError("Expected linux/amd64")
    if config["config"]["Labels"]["org.opencontainers.image.revision"] != expected_sha:
        raise ValueError("OCI source revision does not match the rehearsal target SHA")

    print(json.dumps({
        "source_sha": expected_sha,
        "platform": "linux/amd64",
        "manifest_digest": descriptor["digest"],
        "config_digest": manifest["config"]["digest"],
    }, indent=2))


if __name__ == "__main__":
    main(*sys.argv[1:])
