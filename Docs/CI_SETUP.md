# CI Setup Guide — Unity License Activation

The build pipeline uses [GameCI](https://game.ci) to build the Unity project on GitHub Actions.
A Unity license must be activated once before automated builds will work.

---

## Step 1 — Run the Activation Workflow

1. Go to: **github.com/EarthDeparture/project-condemned/actions**
2. In the left sidebar, click **Unity License Activation**
3. Click **Run workflow** → **Run workflow** (on the `main` branch)
4. Wait ~30 seconds for it to complete
5. Click into the completed run → **Artifacts** → download `Unity_v6000.3.10f1.alf`

---

## Step 2 — Get the License File from Unity

1. Go to: **https://license.unity3d.com/manual**
2. Sign in with your Unity account
3. Upload the `.alf` file you downloaded
4. Unity generates a `.ulf` license file — download it

---

## Step 3 — Add GitHub Secrets

Run these three commands (you will be prompted to paste the value):

```bash
# Paste the full contents of the .ulf file when prompted
gh secret set UNITY_LICENSE --repo EarthDeparture/project-condemned

# Your Unity account email
gh secret set UNITY_EMAIL --repo EarthDeparture/project-condemned

# Your Unity account password
gh secret set UNITY_PASSWORD --repo EarthDeparture/project-condemned
```

For `UNITY_LICENSE`, the value is the **entire contents** of the `.ulf` file (it is XML — paste it all).

---

## Step 4 — Verify

Push any change to `main` or `develop`. The **Unity Build CI** workflow will now run successfully.
The **Script compile check** job runs on every push regardless of license status.

---

## Notes

- The `.alf` and `.ulf` files are **not committed** to the repo — they are sensitive license files.
- If the license expires or the build starts failing with license errors, repeat from Step 1.
- The `activation.yml` workflow is safe to run again at any time.
