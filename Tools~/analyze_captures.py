import cv2
import numpy as np
import json
import sys
from pathlib import Path

def analyze():
    try:
        # Target directory from argument or project root
        if len(sys.argv) > 1:
            captures_dir = Path(sys.argv[1])
        else:
            captures_dir = Path(__file__).parent.parent.parent / "ValidationCaptures"

        print(f"[Python] Scanning directory: {captures_dir}")

        if not captures_dir.exists():
            print(f"[Error] Directory not found: {captures_dir}")
            return

        meta_files = list(captures_dir.glob("*_Meta.json"))
        print(f"[Python] Found {len(meta_files)} captures to analyze.")

        for meta_path in meta_files:
            frame_a = meta_path.with_name(meta_path.name.replace("_Meta.json", "_FrameA.png"))
            frame_b = meta_path.with_name(meta_path.name.replace("_Meta.json", "_FrameB.png"))
            report_path = meta_path.with_name(meta_path.name.replace("_Meta.json", "_REPORT.json"))

            if not frame_a.exists() or not frame_b.exists():
                continue

            img_a = cv2.imread(str(frame_a))
            img_b = cv2.imread(str(frame_b))

            if img_a is None or img_b is None:
                continue

            errors = []

            # 1. Z-Fighting Check (Differential Pixel Analysis)
            diff = cv2.absdiff(img_a, img_b)
            gray = cv2.cvtColor(diff, cv2.COLOR_BGR2GRAY)
            if np.count_nonzero(gray > 25) > 400:
                errors.append("Z-Fighting Detected")

            # 2. Magenta/Missing Shader Check
            # BGR range for Unity's magenta (255, 0, 255)
            mask = cv2.inRange(img_a, np.array([250, 0, 250]), np.array([255, 10, 255]))
            if np.count_nonzero(mask) > 150:
                errors.append("Magenta Texture Detected")

            # 3. Final Report Generation
            if errors:
                print(f"[!] Issue in {meta_path.stem}: {errors}")
                with open(meta_path, 'r') as f:
                    data = json.load(f)
                data['errors'] = errors
                data['is_bug'] = True
                with open(report_path, 'w') as f:
                    json.dump(data, f, indent=4)
            else:
                if report_path.exists():
                    report_path.unlink()

        print("[Python] Analysis cycle completed.")

    except Exception as e:
        print(f"[CRITICAL] Python Crash: {e}")

if __name__ == "__main__":
    analyze()