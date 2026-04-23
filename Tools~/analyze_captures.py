import cv2
import numpy as np
import json
import sys
from pathlib import Path

def analyze():
    try:
        # 1. Obtain the captures directory from command line or use default
        if len(sys.argv) > 1:
            captures_dir = Path(sys.argv[1])
        else:
            captures_dir = Path(__file__).parent / "ValidationCaptures"

        print(f"[Python] Analyzing: {captures_dir}")

        if not captures_dir.exists():
            print(f"[Error] The folder does not exist: {captures_dir}")
            return

        # 2. Search for Meta.json files
        meta_files = list(captures_dir.glob("*_Meta.json"))
        print(f"[Python] Found {len(meta_files)} metadata files.")

        for meta_path in meta_files:
            # Replace the suffix to find the frames
            frame_a = meta_path.with_name(meta_path.name.replace("_Meta.json", "_FrameA.png"))
            frame_b = meta_path.with_name(meta_path.name.replace("_Meta.json", "_FrameB.png"))
            report_path = meta_path.with_name(meta_path.name.replace("_Meta.json", "_REPORT.json"))

            if not frame_a.exists() or not frame_b.exists():
                continue

            # Load images (OpenCV uses strings, Pathlib provides them with str())
            img_a = cv2.imread(str(frame_a))
            img_b = cv2.imread(str(frame_b))

            if img_a is None or img_b is None:
                continue

            errors = []

            # TEST: Z-Fighting (Difference between Frame A and Frame B)
            diff = cv2.absdiff(img_a, img_b)
            gray_diff = cv2.cvtColor(diff, cv2.COLOR_BGR2GRAY)
            # Sensitivity threshold: 20 brightness, > 500 affected pixels
            if np.count_nonzero(gray_diff > 20) > 500:
                errors.append("Z-Fighting detected")

            # TEST: Magenta (Missing Shaders)
            # In OpenCV the order is BGR
            lower_magenta = np.array([250, 0, 250]) 
            upper_magenta = np.array([255, 10, 255])
            mask = cv2.inRange(img_a, lower_magenta, upper_magenta)
            if np.count_nonzero(mask) > 100:
                errors.append("Magenta Texture detected")

            # 3. Save Report if there are errors
            if errors:
                print(f"[!] Error in {meta_path.stem}: {errors}")
                with open(meta_path, 'r') as f:
                    data = json.load(f)
                
                data['errors'] = errors
                data['is_bug'] = True
                
                with open(report_path, 'w') as f:
                    json.dump(data, f, indent=4)
            else:
                # If the error was resolved, delete the old report
                if report_path.exists():
                    report_path.unlink()

        print("[Python] Analysis completed.")
    except Exception as e:
        print(f"[CRASH PYTHON] A critical error occurred: {e}")

if __name__ == "__main__":
    analyze()