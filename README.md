# The Beginning of Nil

A new 2D unity game by CrystalKeeper7

---

## Requirements

- **Unity Hub** (latest)  
- **Unity Editor** version: see `ProjectSettings/ProjectVersion.txt`  
- **Git** (for source control)  
- **Git LFS** (Large File Storage, required for big assets like `.psd`, `.fbx`, `.wav`, etc.)  
  - Install: https://git-lfs.com  

---

## Getting Started

### 1. Clone the repository
```
git clone https://github.com/
<your-username>/the-beginning-of-nil.git
cd the-beginning-of-nil
```

### 2. Set up Git LFS (if not already installed)
```
git lfs install
git lfs pull
```

> If you forget to install LFS before cloning, some large assets (FBX/PSD/WAV) may look like tiny text files. Run the commands above to fetch them properly.

### 3. Open the project in Unity
- Open **Unity Hub**  
- Click **Add Project** (or **Open**) and select the cloned folder  
- Unity will auto-detect the version from `ProjectSettings/ProjectVersion.txt`  

### 4. Let Unity rebuild caches
- On first open, Unity will:  
  - Re-import all assets (`Library/` is regenerated)  
  - Restore packages from `Packages/manifest.json`  
  - Recreate IDE files (`.csproj`, `.sln`)  

This may take a few minutes the first time.  

---

## Repository Structure

- `Assets/` → game scenes, scripts, prefabs, art assets (with `.meta` files)  
- `Packages/` → Unity package dependencies (`manifest.json`, `packages-lock.json`)  
- `ProjectSettings/` → project-wide Unity settings (tags, physics, URP, etc.)  
- `UserSettings/`, `Library/`, `Temp/`, `Obj/`, `Logs/` → **ignored by Git** (local machine caches/preferences)  

---

## 🛠️ Collaboration Notes

- Always **commit `.meta` files** with assets — they preserve references.  
- Commit **both**:  
  - `Packages/manifest.json`  
  - `Packages/packages-lock.json`  
  (ensures package versions are identical across machines)  
- `UserSettings/` is ignored since it only contains per-user editor layouts.  
- If using **URP**, make sure the pipeline asset is assigned (committed in `ProjectSettings/`).  

---

## Common Issues

- **Pink materials** → URP asset not assigned. Check `ProjectSettings → Graphics/Quality`.  
- **Missing Addressables data** → Run `Build → New Build → Default Build Script` locally.  
- **Large assets missing** → Run:  
```
git lfs pull
```

---

## Credits

Developed by CrystalKeeper7.  
Built with Unity.  
