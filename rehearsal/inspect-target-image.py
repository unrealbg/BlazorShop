"""Verify a Linux/amd64 OCI archive, its blobs and its source revision."""

import hashlib
import json
import re
import sys
import tarfile


def blob_member(archive, descriptor):
    digest = descriptor["digest"]
    if not re.fullmatch(r"sha256:[0-9a-f]{64}", digest):
        raise ValueError(f"Invalid OCI digest: {digest}")
    member = archive.getmember(f"blobs/sha256/{digest.split(':')[1]}")
    if not member.isfile() or member.size != descriptor["size"]:
        raise ValueError(f"OCI blob size or type does not match {digest}")
    return member


def verify_digest(actual, digest):
    if digest != "sha256:" + actual:
        raise ValueError(f"OCI blob does not match {digest}")


def read_json_blob(archive, descriptor):
    with archive.extractfile(blob_member(archive, descriptor)) as blob:
        data = blob.read()
    verify_digest(hashlib.sha256(data).hexdigest(), descriptor["digest"])
    return json.loads(data)


def main(path, expected_sha):
    with open(path, "rb") as source:
        archive_sha256 = hashlib.file_digest(source, "sha256").hexdigest()
        source.seek(0)
        with tarfile.open(fileobj=source, mode="r:") as archive:
            with archive.extractfile("index.json") as index:
                (descriptor,) = json.load(index)["manifests"]
            if descriptor["mediaType"] != "application/vnd.oci.image.manifest.v1+json":
                raise ValueError("Expected a single OCI image manifest")
            manifest = read_json_blob(archive, descriptor)
            config = read_json_blob(archive, manifest["config"])
            for layer in manifest["layers"]:
                with archive.extractfile(blob_member(archive, layer)) as blob:
                    verify_digest(hashlib.file_digest(blob, "sha256").hexdigest(), layer["digest"])

    if (config["os"], config["architecture"]) != ("linux", "amd64"):
        raise ValueError("Expected linux/amd64")
    if config["config"]["Labels"]["org.opencontainers.image.revision"] != expected_sha:
        raise ValueError("OCI source revision does not match the rehearsal target SHA")

    print(json.dumps({
        "source_sha": expected_sha,
        "platform": "linux/amd64",
        "archive_sha256": archive_sha256,
        "manifest_digest": descriptor["digest"],
        "config_digest": manifest["config"]["digest"],
    }, indent=2))


if __name__ == "__main__":
    main(*sys.argv[1:])
