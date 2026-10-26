"""{
  "責務": "PR前のローカル検査を起動します。"
}"""

from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent / "tools/local-check"))
from local_check import main

if __name__ == "__main__":
    raise SystemExit(main(Path(__file__).resolve().parent))
