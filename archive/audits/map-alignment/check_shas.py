import hashlib, json
root = r"G:\Proyecto Planos\phm\phm\uifigmastyle_UX_REDESIGN\Resources"
files = {
    "norte": f"{root}\\plano_norte_limpio.svg",
    "nivel3": f"{root}\\plano_nivel3_limpio.svg",
    "sur": f"{root}\\plano_sur_limpio.svg",
    "id": f"{root}\\plano_id.svg",
    "qc": f"{root}\\plano_qc_limpio.svg",
}
for name, path in files.items():
    with open(path, "rb") as f:
        h = hashlib.sha256(f.read()).hexdigest()
    print(f"{name}: {h}")