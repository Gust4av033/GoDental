#!/usr/bin/env python3
"""Verifica integridad del inventario aportado; no acredita vigencia ni habilita DTE."""

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sys


MANIFEST = Path("docs/FIS-01/fuentes-oficiales.json")
RESERVED = {"CON", "PRN", "AUX", "NUL"} | {
    f"{prefix}{number}" for prefix in ("COM", "LPT") for number in range(1, 10)
}


def source_path(root, value):
    """Rutas relativas portables en Windows, sin escape ni enlaces simbólicos."""
    if not isinstance(value, str) or not value:
        raise ValueError("Ruta de fuente inválida")
    parts = value.split("/")
    if PurePosixPath(value).is_absolute() or parts[0] != "DTEPDF":
        raise ValueError("Fuente fuera de DTEPDF")
    for part in parts:
        if (not part or part in (".", "..") or part.endswith((".", " "))
                or any(ord(c) < 32 or c in '<>:"\\|?*' for c in part)
                or part.split(".")[0].upper() in RESERVED):
            raise ValueError("Ruta de fuente no portable o insegura")
    target = root
    for part in parts:
        target = target / part
        if target.is_symlink():
            raise ValueError("Enlace simbólico no permitido en fuentes")
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError("Fuente resuelta fuera del repositorio")
    return target


def verify(root):
    root = Path(root).resolve()
    errors = []
    checked = 0
    expected = set()
    missing = []
    try:
        manifest = json.loads((root / MANIFEST).read_text(encoding="utf-8"))
        if not isinstance(manifest, dict):
            raise ValueError("Inventario inválido")
        if manifest.get("format_version") != 1:
            raise ValueError("Versión de inventario no soportada")
        sources = manifest["sources"]
        contracts = manifest["missing_contracts"]
        if not isinstance(sources, list) or not sources or not isinstance(contracts, list):
            raise ValueError("Inventario incompleto")
        ids = set()
        for item in sources:
            identifier = item["id"]
            if not isinstance(identifier, str) or identifier in ids:
                raise ValueError("ID de fuente inválido o duplicado")
            ids.add(identifier)
            target = source_path(root, item["path"])
            if item["path"] in expected:
                raise ValueError("Ruta de fuente duplicada")
            expected.add(item["path"])
            digest = item["sha256"]
            size = item["bytes"]
            if (not isinstance(digest, str) or not re.fullmatch(r"[a-f0-9]{64}", digest)
                    or type(size) is not int or size <= 0):
                raise ValueError("Hash o tamaño de fuente inválido")
            if not target.is_file():
                errors.append(f"{identifier}: archivo ausente")
                continue
            data = target.read_bytes()
            if len(data) != size or hashlib.sha256(data).hexdigest() != digest:
                errors.append(f"{identifier}: tamaño o SHA-256 no coincide")
            else:
                checked += 1
        missing = [item["id"] for item in contracts]
        if any(not isinstance(identifier, str) for identifier in missing):
            raise ValueError("Contrato pendiente inválido")
        found = {
            p.relative_to(root).as_posix()
            for p in (root / "DTEPDF").rglob("*")
            if p.suffix.lower() == ".pdf"
        }
        if found - expected:
            errors.append("Hay PDF sin inventariar en DTEPDF")
        # En Windows, exists('DTEPDF.') también puede resolver 'DTEPDF'. Comparar nombres.
        if any(p.name == "DTEPDF." for p in root.iterdir()):
            errors.append("Existe carpeta DTEPDF. no portable en Windows")
    except (OSError, ValueError, KeyError, TypeError) as error:
        # No volcar contenido de archivos ni objetos del inventario a logs de CI.
        errors.append(f"No se pudo verificar inventario: {type(error).__name__}")
    return {"verified_sources": checked, "errors": errors, "missing_contracts": missing,
            "scope": "Integridad local; no acredita origen, vigencia ni autorización fiscal"}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--report", type=Path)
    parser.add_argument("--require-contracts", action="store_true",
                        help="Falla si el inventario registra contratos pendientes; no es un preflight PROD")
    args = parser.parse_args(argv)
    report = verify(args.root)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Fuentes con SHA-256 correcto: {report['verified_sources']}")
    for error in report["errors"]:
        print(error, file=sys.stderr)
    if report["errors"]:
        return 1
    if report["missing_contracts"]:
        print("PENDIENTE_DE_DOCUMENTACION_OFICIAL: " + ", ".join(report["missing_contracts"]))
        if args.require_contracts:
            return 2
    print(report["scope"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
