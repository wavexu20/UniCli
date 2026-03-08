#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_DIR="${UNICLI_PROJECT:-$REPO_ROOT/src/UniCli.Unity}"
if [[ "$PROJECT_DIR" != /* ]]; then
  PROJECT_DIR="$REPO_ROOT/$PROJECT_DIR"
fi
export UNICLI_PROJECT="$PROJECT_DIR"

TMP_ROOT="${TMPDIR:-/tmp}"
WORK_DIR="$TMP_ROOT/unicli-uitree-smoke-$(date +%s)"
mkdir -p "$WORK_DIR"

run_unicli() {
  local output
  local status=0

  if command -v unicli >/dev/null 2>&1; then
    UNICLI_BASE=(unicli)
  else
    UNICLI_BASE=(dotnet run --no-build --project "$REPO_ROOT/src/UniCli.Client/UniCli.Client.csproj" --)
  fi

  if ! "${UNICLI_BASE[@]}" check 2>/dev/null | grep -Eq 'Server:[[:space:]]+running'; then
    local unity_app="${UNICLI_UNITY_APP:-}"
    if [[ -z "$unity_app" ]]; then
      for candidate in /Applications/Unity/Unity-*/Unity.app; do
        if [[ -d "$candidate" ]]; then
          unity_app="$candidate"
          break
        fi
      done
    fi

    if [[ -n "$unity_app" && -d "$unity_app" ]]; then
      open -a "$unity_app" --args -projectPath "$PROJECT_DIR" >/dev/null 2>&1 || true
      for _ in {1..60}; do
        if "${UNICLI_BASE[@]}" check 2>/dev/null | grep -Eq 'Server:[[:space:]]+running'; then
          break
        fi
        sleep 2
      done
    fi
  fi

  set +e
  output="$("${UNICLI_BASE[@]}" "$@" 2>&1)"
  status=$?
  set -e
  printf '%s\n' "$output"
  return $status
}

cleanup_unity_fixture() {
  run_unicli eval "
var titles = new[]
{
  \"UniCli UITree Test\",
  \"UniCli Mixed Capture Test\",
  \"UniCli Pure IMGUI Capture Test\"
};

var windows = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>();
foreach (var w in windows)
{
  if (w != null && w.titleContent != null && System.Array.IndexOf(titles, w.titleContent.text) >= 0)
  {
    w.Close();
  }
}

var root = UnityEngine.GameObject.Find(\"UniCliSmokeRoot\");
if (root != null) UnityEngine.Object.DestroyImmediate(root);

var materials = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Material>();
foreach (var material in materials)
{
  if (material != null && material.name != null && material.name.StartsWith(\"UniCliSmokeMat_\"))
  {
    UnityEngine.Object.DestroyImmediate(material);
  }
}

return \"ok\";
" --json >/dev/null 2>&1 || true
}

cleanup() {
  cleanup_unity_fixture
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

run_json() {
  local output
  output=$("$@")
  echo "$output" | grep -Eq '"success"[[:space:]]*:[[:space:]]*true'
  echo "$output"
}

echo "[smoke] checking UniCli connection"
run_unicli check >/dev/null

echo "[smoke] creating temporary Editor UI fixtures"
run_json run_unicli eval "
UnityEditor.EditorWindow OpenFixture(string fullName)
{
  var type = System.AppDomain.CurrentDomain.GetAssemblies()
    .SelectMany(assembly =>
    {
      try { return assembly.GetTypes(); }
      catch (System.Reflection.ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
    })
    .FirstOrDefault(type => type != null && type.FullName == fullName);

  if (type == null)
  {
    throw new System.InvalidOperationException(\"Fixture type not found: \" + fullName);
  }

  var openMethod = type.GetMethod(\"Open\", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
  if (openMethod == null)
  {
    throw new System.InvalidOperationException(\"Open() not found on fixture type: \" + fullName);
  }

  return (UnityEditor.EditorWindow)openMethod.Invoke(null, null);
}

UniCli.Server.Editor.Tests.UITreeTestEditorWindow.Open();
OpenFixture(\"UniCli.Server.Editor.Tests.MixedCaptureTestEditorWindow\");
OpenFixture(\"UniCli.Server.Editor.Tests.PureImguiCaptureTestEditorWindow\");
return \"fixtures-ready\";
" --json >/dev/null

echo "[smoke] creating temporary camera"
run_json run_unicli eval "
UnityEngine.Material CreateMaterial(string name, UnityEngine.Color color)
{
  var shader = UnityEngine.Shader.Find(\"Universal Render Pipeline/Lit\") ?? UnityEngine.Shader.Find(\"Standard\");
  var material = new UnityEngine.Material(shader);
  material.name = name;
  material.color = color;
  return material;
}

var old = UnityEngine.GameObject.Find(\"UniCliSmokeRoot\");
if (old != null) UnityEngine.Object.DestroyImmediate(old);

var root = new UnityEngine.GameObject(\"UniCliSmokeRoot\");

var cameraGo = new UnityEngine.GameObject(\"UniCliSmokeCamera\");
cameraGo.transform.SetParent(root.transform);
var camera = cameraGo.AddComponent<UnityEngine.Camera>();
camera.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
camera.backgroundColor = new UnityEngine.Color(0.16f, 0.17f, 0.24f);
camera.transform.position = new UnityEngine.Vector3(0f, 1.8f, -5.5f);
camera.transform.LookAt(new UnityEngine.Vector3(0f, 0.9f, 0f));

var lightGo = new UnityEngine.GameObject(\"UniCliSmokeLight\");
lightGo.transform.SetParent(root.transform);
var light = lightGo.AddComponent<UnityEngine.Light>();
light.type = UnityEngine.LightType.Directional;
light.intensity = 1.2f;
light.transform.rotation = UnityEngine.Quaternion.Euler(40f, -35f, 0f);

var plane = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Plane);
plane.name = \"UniCliSmokePlane\";
plane.transform.SetParent(root.transform);
plane.transform.localScale = new UnityEngine.Vector3(0.5f, 1f, 0.5f);
plane.GetComponent<UnityEngine.Renderer>().sharedMaterial = CreateMaterial(\"UniCliSmokeMat_Plane\", new UnityEngine.Color(0.28f, 0.30f, 0.34f));

var cube = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
cube.name = \"UniCliSmokeCube\";
cube.transform.SetParent(root.transform);
cube.transform.position = new UnityEngine.Vector3(-1.2f, 0.6f, 0f);
cube.GetComponent<UnityEngine.Renderer>().sharedMaterial = CreateMaterial(\"UniCliSmokeMat_Cube\", new UnityEngine.Color(0.91f, 0.33f, 0.22f));

var sphere = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);
sphere.name = \"UniCliSmokeSphere\";
sphere.transform.SetParent(root.transform);
sphere.transform.position = new UnityEngine.Vector3(1.1f, 0.75f, 0.4f);
sphere.GetComponent<UnityEngine.Renderer>().sharedMaterial = CreateMaterial(\"UniCliSmokeMat_Sphere\", new UnityEngine.Color(0.23f, 0.58f, 0.93f));

return cameraGo.name;
" --json >/dev/null

echo "[smoke] running UITree commands"
run_json run_unicli exec UITree.Dump --panel "UniCli UITree Test" --depth 10 --json >/dev/null
run_json run_unicli exec UITree.Inspect --panel "UniCli UITree Test" --selector "#name-field" --json >/dev/null

EDITOR_IMAGE="$WORK_DIR/editor.png"
EDITOR_CHANGED_IMAGE="$WORK_DIR/editor_changed.png"
EDITOR_CROP_IMAGE="$WORK_DIR/editor_crop.png"
MIXED_IMAGE="$WORK_DIR/mixed.png"
PURE_IMGUI_IMAGE="$WORK_DIR/pure_imgui.png"
CAMERA_IMAGE_A="$WORK_DIR/camera_a.png"
CAMERA_IMAGE_B="$WORK_DIR/camera_b.png"
CAMERA_IMAGE_TRANSPARENT="$WORK_DIR/camera_transparent.png"

echo "[smoke] running screenshot commands"
run_json run_unicli exec Screenshot.CaptureEditor --panel "UniCli UITree Test" --filename "$EDITOR_IMAGE" --json >/dev/null
run_json run_unicli exec UITree.Click --panel "UniCli UITree Test" --selector "#run-button" --json >/dev/null
run_json run_unicli exec UITree.Fill --panel "UniCli UITree Test" --selector "#name-field" --value "Updated by smoke" --json >/dev/null
run_json run_unicli exec UITree.Select --panel "UniCli UITree Test" --selector "#mode-dropdown" --choice "Gamma" --json >/dev/null
DIFF_OUTPUT="$(run_json run_unicli exec Screenshot.CaptureEditor --panel "UniCli UITree Test" --filename "$EDITOR_CHANGED_IMAGE" --diffBase "$EDITOR_IMAGE" --json)"
echo "$DIFF_OUTPUT" | grep -q '"percentage"'
DIFF_IMAGE="$(echo "$DIFF_OUTPUT" | sed -n 's/.*"diffImagePath":[[:space:]]*"\([^"]*\)".*/\1/p' | head -n1)"
echo "$DIFF_OUTPUT" | grep -Eq '"changedPixels"[[:space:]]*:[[:space:]]*[1-9][0-9]*'
run_json run_unicli exec Screenshot.CaptureEditor --panel "UniCli UITree Test" --selector "#preview-swatch" --filename "$EDITOR_CROP_IMAGE" --json >/dev/null
run_json run_unicli exec Screenshot.CaptureEditor --panel "UniCli Mixed Capture Test" --filename "$MIXED_IMAGE" --json >/dev/null
run_json run_unicli exec Screenshot.CaptureEditor --panel "UniCli Pure IMGUI Capture Test" --filename "$PURE_IMGUI_IMAGE" --json >/dev/null
run_json run_unicli exec Screenshot.CaptureCamera --cameraName "UniCliSmokeCamera" --width 640 --height 360 --filename "$CAMERA_IMAGE_A" --json >/dev/null
run_json run_unicli eval "
var cube = UnityEngine.GameObject.Find(\"UniCliSmokeCube\");
if (cube != null) cube.transform.position = new UnityEngine.Vector3(-0.1f, 1.4f, -0.2f);
var sphere = UnityEngine.GameObject.Find(\"UniCliSmokeSphere\");
if (sphere != null) sphere.GetComponent<UnityEngine.Renderer>().sharedMaterial.color = new UnityEngine.Color(0.95f, 0.78f, 0.25f);
return \"ok\";
" --json >/dev/null
run_json run_unicli exec Screenshot.CaptureCamera --cameraName "UniCliSmokeCamera" --width 640 --height 360 --filename "$CAMERA_IMAGE_B" --json >/dev/null
run_json run_unicli exec Screenshot.CaptureCamera --cameraName "UniCliSmokeCamera" --width 640 --height 360 --filename "$CAMERA_IMAGE_TRANSPARENT" --transparent --json >/dev/null

if [[ ! -f "$EDITOR_IMAGE" ]]; then
  echo "Editor image not generated: $EDITOR_IMAGE" >&2
  exit 1
fi

if [[ -z "$DIFF_IMAGE" || ! -f "$DIFF_IMAGE" ]]; then
  echo "Diff image not generated: $DIFF_IMAGE" >&2
  exit 1
fi

for image in "$EDITOR_CROP_IMAGE" "$MIXED_IMAGE" "$PURE_IMGUI_IMAGE" "$CAMERA_IMAGE_A" "$CAMERA_IMAGE_B" "$CAMERA_IMAGE_TRANSPARENT"; do
  if [[ ! -f "$image" ]]; then
    echo "Expected image not generated: $image" >&2
    exit 1
  fi
done

if cmp -s "$CAMERA_IMAGE_A" "$CAMERA_IMAGE_B"; then
  echo "Camera images should differ after scene mutation" >&2
  exit 1
fi

echo "[smoke] passed"
