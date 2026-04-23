# Visual Validator for Unity

**Visual Validator** is a high-caliber automation pipeline designed for massive visual bug detection (Z-Fighting, Missing Shaders/Magenta textures, Empty Scenes) within Unity projects. It utilizes a hybrid workflow combining the Unity engine with **OpenCV** image analysis.

---

## 🚀 Key Features

* **Multi-Level Grid Generation:** Automatically scans complex volumes and generates view points over traversable surfaces.
* **Batchmode Scanning:** Automatically closes the editor to run an ultra-fast headless sweep of all scenes in the background.
* **Python-Powered Analysis:** Precise detection of visual glitches by comparing pixel jittering and color ranges.
* **Integrated Report Explorer:** View detected issues side-by-side (Frame A vs. Frame B) and teleport the editor camera to the exact bug location 1:1.

---

## 🛠️ Prerequisites

1.  **Unity 6** or higher.
2.  **Python 3.x** installed and added to your Windows `PATH`.
3.  **OpenCV** library for Python. You can install it via:
    ```bash
    pip install opencv-python numpy
    ```

---

## 📦 Installation

### Via Unity Package Manager (Git URL)
1.  In Unity, go to `Window > Package Manager`.
2.  Click the `+` icon and select `Add package from git URL...`.
3.  Paste the repository URL:
    `https://github.com/your-username/com.visual.validator.git`

---

## 📖 How to Use

### 1. Initial Setup (Critical)
Due to permission restrictions in the Unity Package Cache (`Library/PackageCache`), you must extract the execution tools to your project root:
* Open `Window > Visual Validator > Control Panel`.
* Click **EXTRACT TOOLS TO PROJECT ROOT**.
* This will create a `/ValidationTools/` folder in your project's root directory.

### 2. Scene Preparation
* Add all scenes you wish to validate to `File > Build Settings`.
* In your scene, create an object with the `ScanAreaGenerator` script.
* Configure the area and click **Generate Multi-Level Points**.
* **Save your scene (`Ctrl + S`) before proceeding.**

### 3. Running the Pipeline
* In the `Control Panel`, click **▶ RUN VALIDATION PIPELINE**.
* **Unity will close automatically.**
* A CMD console will open and perform:
    1.  Headless frame capture in Unity.
    2.  Image analysis via Python.
    3.  **Unity will restart automatically** once finished.

### 4. Reviewing Results
* Open `Window > Visual Validator > Report Explorer`.
* If bugs were detected, they will appear in the list.
* Click **INSPECT IMAGES** to see the comparison.
* Click **TELEPORT TO SOURCE** to align your Editor camera exactly with the capture perspective.

---

## 📂 Project Structure

* `/Packages/com.visual.validator`: Source code and package logic.
* `/ValidationTools/`: (Generated) `.bat` and `.py` scripts for external execution.
* `/ValidationCaptures/`: (Generated) Storage for screenshots, metadata, and error reports.

---

## 🛡️ Important Notes
* The script closes Unity to release file locks on materials and assets, allowing the Python analyzer to work without system conflicts.
* Ensure your project is free of **Compiler Errors** before launching the validation, as the `batchmode` process will fail otherwise.

---
*Developed by Fabio González Trujillo - 2026*
