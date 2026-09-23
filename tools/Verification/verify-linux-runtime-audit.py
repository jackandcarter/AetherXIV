#!/usr/bin/env python3
"""Check audit search paths and failure reporting without executing Wine."""
import os
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
with tempfile.TemporaryDirectory(prefix="aetherxiv-linkage-test-") as temporary:
    work = Path(temporary)
    bundle = work / "runtime with spaces"
    mocks = work / "commands"
    mocks.mkdir()
    required = [
        "bin/wine", "bin/wineserver",
        "lib/wine/i386-windows/winevulkan.dll",
        "lib/wine/x86_64-windows/winevulkan.dll",
        "lib/wine/x86_64-unix/winevulkan.so",
        "lib/wine/x86_64-unix/ntdll.so",
        "lib/wine/x86_64-unix/win32u.so",
        "lib/wine/i386-windows/xaudio2_4.dll",
        "lib/wine/i386-windows/xaudio2_7.dll",
        "lib/wine/i386-windows/xapofx1_5.dll",
        "lib/wine/i386-windows/winepulse.drv",
        "lib/wine/x86_64-unix/winepulse.so",
        "lib/wine/i386-windows/d3d9.dll",
        "dxvk/x32/d3d9.dll", "dxvk/probe/AetherXIV.DxvkProbe.exe",
    ]
    for relative in required:
        path = bundle / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.touch()
        path.chmod(0o755)
    (mocks / "uname").write_text("#!/bin/sh\necho x86_64\n")
    (mocks / "ldd").write_text('''#!/bin/sh
case "$LD_LIBRARY_PATH" in
  "$EXPECTED_LIBRARY_DIR"|"$EXPECTED_LIBRARY_DIR":*) ;;
  *) echo 'ntdll.so => not found'; exit 0 ;;
esac
case "$AUDIT_TEST_MODE" in
  missing) echo 'libpulse.so.0 => not found'; exit 0 ;;
  error) echo 'invalid ELF header' >&2; exit 1 ;;
esac
echo 'ntdll.so => bundled/ntdll.so'
''')
    for path in mocks.iterdir():
        path.chmod(0o755)
    env = dict(os.environ, PATH=str(mocks) + os.pathsep + os.environ["PATH"],
               EXPECTED_LIBRARY_DIR=str(bundle / "lib/wine/x86_64-unix"))
    for mode, expected, message in [("ok", 0, "audit passed"),
                                    ("missing", 1, "libpulse.so.0 => not found"),
                                    ("error", 1, "invalid ELF header")]:
        result = subprocess.run(["bash", str(ROOT / "tools/runtime/audit-linux-bundle.sh"), str(bundle)],
                                env=dict(env, AUDIT_TEST_MODE=mode), capture_output=True, text=True)
        assert result.returncode == expected, result.stdout + result.stderr
        assert message in result.stdout + result.stderr, result.stdout + result.stderr
        print(f"PASS: {mode}")
    (bundle / "lib/wine/x86_64-unix/ntdll.so").unlink()
    result = subprocess.run(["bash", str(ROOT / "tools/runtime/audit-linux-bundle.sh"), str(bundle)],
                            env=dict(env, AUDIT_TEST_MODE="ok"), capture_output=True, text=True)
    assert result.returncode == 1 and "missing lib/wine/x86_64-unix/ntdll.so" in result.stderr
    print("PASS: missing bundled ntdll rejected")
