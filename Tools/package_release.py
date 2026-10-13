#!/usr/bin/env python3
"""Build standalone Unity package tarballs and a combined download."""

import argparse
import gzip
import json
import tarfile
import tempfile
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PACKAGES = {
    "core": ROOT / "Assets/Packages/com.collint.mmd",
    "samples": ROOT / "Assets/Packages/com.collint.mmd.samples",
}


def package_files(directory):
    files = []
    for path in sorted(directory.rglob("*")):
        relative = path.relative_to(directory)
        if any(part.startswith(".") or part.endswith("~") for part in relative.parts):
            continue
        if path.is_symlink():
            raise ValueError(f"Package contains a symbolic link: {relative}")
        if not path.is_file():
            continue
        with path.open("rb") as stream:
            if stream.read(43).startswith(b"version https://git-lfs.github.com/spec/v1"):
                raise ValueError(f"Git LFS content is missing: {path}. Run git lfs pull first.")
        files.append(path)
    return files


def pack(directory, output):
    manifest = json.loads((directory / "package.json").read_text())
    target = output / f"{manifest['name']}-{manifest['version']}.tgz"
    files = package_files(directory)
    with tempfile.TemporaryDirectory(dir=output) as temporary:
        staging = Path(temporary) / target.name
        with staging.open("wb") as stream:
            with gzip.GzipFile(filename="", mode="wb", fileobj=stream, mtime=0) as compressed:
                with tarfile.open(fileobj=compressed, mode="w|") as archive:
                    for path in files:
                        info = archive.gettarinfo(str(path), "package/" + path.relative_to(directory).as_posix())
                        info.uid = info.gid = 0
                        info.uname = info.gname = ""
                        info.mtime = 0
                        info.mode = 0o644
                        with path.open("rb") as source:
                            archive.addfile(info, source)
        staging.replace(target)
    print(f"{target.relative_to(ROOT) if target.is_relative_to(ROOT) else target}: {target.stat().st_size:,} bytes", flush=True)
    return target


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", choices=["core", "samples", "all"], default="all")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/packages")
    args = parser.parse_args()
    core = json.loads((PACKAGES["core"] / "package.json").read_text())
    samples = json.loads((PACKAGES["samples"] / "package.json").read_text())
    if samples["dependencies"][core["name"]] != core["version"]:
        raise ValueError("Samples must depend on the current core version.")
    output = args.output.resolve()
    if any(output.is_relative_to(directory) for directory in PACKAGES.values()):
        raise ValueError("Output must be outside the package source directories.")
    output.mkdir(parents=True, exist_ok=True)
    selected = PACKAGES if args.package == "all" else {args.package: PACKAGES[args.package]}
    archives = [pack(directory, output) for directory in selected.values()]
    if args.package == "all":
        target = output / f"mmd-with-samples-{core['version']}.zip"
        with tempfile.TemporaryDirectory(dir=output) as temporary:
            staging = Path(temporary) / target.name
            with zipfile.ZipFile(staging, "w", compression=zipfile.ZIP_STORED, allowZip64=True) as bundle:
                for archive in archives:
                    bundle.write(archive, archive.name)
                bundle.writestr("INSTALL.txt", "Unity 6000.3+\n\nIn Package Manager, select Install package from tarball.\nInstall com.collint.mmd first, then com.collint.mmd.samples.\nKeep the tarballs at their installed paths.\n\nSamples contain third-party assets; see their Third Party Notices.md.\n")
            staging.replace(target)
        print(f"{target}: {target.stat().st_size:,} bytes", flush=True)


if __name__ == "__main__":
    main()
