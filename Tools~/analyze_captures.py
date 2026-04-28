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
            # Escalado profesional: Usamos resolve() para evitar problemas con paths relativos
            captures_dir = Path(__file__).resolve().parent.parent.parent / "ValidationCaptures"

        print(f"[Python] Scanning directory: {captures_dir}")

        if not captures_dir.exists():
            print(f"[Error] Directory not found: {captures_dir}")
            return

        meta_files = list(captures_dir.glob("*_Meta.json"))
        print(f"[Python] Found {len(meta_files)} captures to analyze.")

        for meta_path in meta_files:
            # Reemplazos más limpios usando .with_name
            base_name = meta_path.name.replace("_Meta.json", "")
            frame_a = meta_path.with_name(f"{base_name}_FrameA.png")
            frame_b = meta_path.with_name(f"{base_name}_FrameB.png")
            report_path = meta_path.with_name(f"{base_name}_REPORT.json")

            if not frame_a.exists() or not frame_b.exists():
                continue

            img_a = cv2.imread(str(frame_a))
            img_b = cv2.imread(str(frame_b))

            if img_a is None or img_b is None:
                print(f"[Warning] Could not read images for {base_name}")
                continue

            errors = []

            # ---------------------------------------------------------
            # 1. Z-Fighting Check (Comentado a propósito como pediste)
            # El Z-fighting suele manifestarse como parpadeo entre Frame A y B
            # si la cámara se movió una fracción o si el render order es inestable.
            # diff = cv2.absdiff(img_a, img_b)
            # gray = cv2.cvtColor(diff, cv2.COLOR_BGR2GRAY)
            # if np.count_nonzero(gray > 25) > 400:
            #    errors.append("Z-Fighting Detected")
            # ---------------------------------------------------------

            # 2. Magenta/Missing Shader Check
            # En Unity, el magenta es (255, 0, 255). En BGR es [255, 0, 255]
            lower_magenta = np.array([240, 0, 240]) 
            upper_magenta = np.array([255, 20, 255])
            mask = cv2.inRange(img_a, lower_magenta, upper_magenta)
            
            if np.count_nonzero(mask) > 150:
                errors.append("Magenta Texture Detected")

            # 3. Final Report Generation
            if errors:
                print(f"[!] Issue in {meta_path.stem}: {errors}")
                
                # Manejo de JSON más seguro
                data = {}
                if meta_path.stat().st_size > 0:
                    with open(meta_path, 'r', encoding='utf-8') as f:
                        try:
                            data = json.load(f)
                        except json.JSONDecodeError:
                            print(f"[Error] Failed to decode {meta_path}")

                data['errors'] = errors
                data['is_bug'] = True
                
                with open(report_path, 'w', encoding='utf-8') as f:
                    json.dump(data, f, indent=4)
            else:
                # Cleanup: Si antes había un bug y ya no, borramos el reporte
                if report_path.exists():
                    report_path.unlink()

        print("[Python] Analysis cycle completed.")

    except Exception as e:
        print(f"[CRITICAL] Python Crash: {e}")

if __name__ == "__main__":
    analyze()