"""Ensayos del verificador documental, sin documentos ni credenciales reales de Hacienda."""

import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location(
    "fiscal_sources", Path(__file__).resolve().parents[2] / "scripts/verify-fiscal-sources.py")
SOURCES = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SOURCES)


class SourceIntegrityTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.pdf = self.root / "DTEPDF" / "Fuente pública.pdf"
        self.pdf.parent.mkdir()
        self.pdf.write_bytes(b"fixture sintetico de integridad")
        self.manifest = {
            "format_version": 1,
            "sources": [{"id": "F01", "path": "DTEPDF/Fuente pública.pdf",
                         "bytes": self.pdf.stat().st_size,
                         "sha256": hashlib.sha256(self.pdf.read_bytes()).hexdigest()}],
            "missing_contracts": [{"id": "T01"}],
        }
        self.save()

    def save(self):
        target = self.root / SOURCES.MANIFEST
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(self.manifest), encoding="utf-8")

    def test_intact_sources_do_not_claim_official_readiness(self):
        report = SOURCES.verify(self.root)
        self.assertEqual([], report["errors"])
        self.assertEqual(1, report["verified_sources"])
        self.assertEqual(["T01"], report["missing_contracts"])

    def test_changed_bytes_fail_even_with_same_length(self):
        self.pdf.write_bytes(b"X" * self.pdf.stat().st_size)
        self.assertTrue(SOURCES.verify(self.root)["errors"])

    def test_missing_source_fails(self):
        self.pdf.unlink()
        self.assertTrue(SOURCES.verify(self.root)["errors"])

    def test_uninventoried_pdf_fails(self):
        (self.pdf.parent / "extra.PDF").write_bytes(b"extra")
        self.assertTrue(SOURCES.verify(self.root)["errors"])

    def test_unsafe_or_nonportable_paths_fail(self):
        for path in ("DTEPDF/../fuera.pdf", "/fuera.pdf", "DTEPDF./x.pdf",
                     "DTEPDF/NUL.pdf", "DTEPDF/x./x.pdf", "DTEPDF/x\\y.pdf",
                     "DTEPDF/x:ads.pdf", "DTEPDF//x.pdf"):
            with self.subTest(path=path), self.assertRaises(ValueError):
                SOURCES.source_path(self.root, path)

    def test_duplicate_inventory_entry_fails(self):
        self.manifest["sources"].append(dict(self.manifest["sources"][0]))
        self.save()
        self.assertTrue(SOURCES.verify(self.root)["errors"])

    def test_malformed_manifest_fails(self):
        (self.root / SOURCES.MANIFEST).write_text("{", encoding="utf-8")
        self.assertTrue(SOURCES.verify(self.root)["errors"])

    def test_require_contracts_is_blocked_by_missing_documents(self):
        self.assertEqual(2, SOURCES.main(["--root", str(self.root), "--require-contracts"]))


if __name__ == "__main__":
    unittest.main()
