#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_DIR="${UNICLI_PROJECT:-$REPO_ROOT/src/UniCli.Unity}"
if [[ "$PROJECT_DIR" != /* ]]; then
  PROJECT_DIR="$REPO_ROOT/$PROJECT_DIR"
fi
export UNICLI_PROJECT="$PROJECT_DIR"

TIMESTAMP="$(date '+%Y%m%d_%H%M%S')"
OUT_DIR="${VERIFY_OUT_DIR:-$REPO_ROOT/artifacts/verification/uitree/$TIMESTAMP}"
mkdir -p "$OUT_DIR"

if command -v unicli >/dev/null 2>&1; then
  UNICLI_CMD=(unicli)
else
  UNICLI_CMD=(dotnet run --no-build --project "$REPO_ROOT/src/UniCli.Client/UniCli.Client.csproj" --)
fi

UNITY_APP="${UNICLI_UNITY_APP:-}"
if [[ -z "$UNITY_APP" ]]; then
  for candidate in /Applications/Unity/Unity-*/Unity.app; do
    if [[ -d "$candidate" ]]; then
      UNITY_APP="$candidate"
      break
    fi
  done
fi

IMAGE_PROBE="$OUT_DIR/image_probe.swift"

run_unicli_raw() {
  "${UNICLI_CMD[@]}" "$@"
}

server_is_running() {
  local output
  output="$(run_unicli_raw check 2>/dev/null || true)"
  grep -Eq 'Server:[[:space:]]+running' <<<"$output"
}

launch_unity_direct() {
  if [[ -z "$UNITY_APP" || ! -d "$UNITY_APP" ]]; then
    return 1
  fi

  open -a "$UNITY_APP" --args -projectPath "$PROJECT_DIR" >/dev/null 2>&1 || return 1
}

wait_for_server() {
  local attempt
  for attempt in {1..60}; do
    if server_is_running; then
      return 0
    fi
    sleep 2
  done

  return 1
}

ensure_server_running() {
  if server_is_running; then
    return 0
  fi

  launch_unity_direct || return 1
  wait_for_server
}

run_unicli() {
  local output
  local status=0

  if [[ "${1:-}" != "check" ]]; then
    ensure_server_running || true
  fi

  set +e
  output="$(run_unicli_raw "$@" 2>&1)"
  status=$?
  set -e

  if [[ $status -ne 0 ]] && [[ "$output" == *"Unity is not running, launching..."* || "$output" == *"Failed to launch Unity Editor"* || "$output" == *"Server disconnected"* || "$output" == *"Waiting for server..."* ]]; then
    ensure_server_running || true
    set +e
    output="$(run_unicli_raw "$@" 2>&1)"
    status=$?
    set -e
  fi

  printf '%s\n' "$output"
  return $status
}

write_command_file() {
  local file="$1"
  shift
  printf '%s\n' "$*" >"$file"
}

run_text_step() {
  local name="$1"
  shift
  local output_file="$OUT_DIR/$name.log"
  local command_file="$OUT_DIR/$name.command.txt"
  write_command_file "$command_file" "$@"
  "$@" >"$output_file" 2>&1
}

run_json_step() {
  local name="$1"
  shift
  local output_file="$OUT_DIR/$name.json"
  local command_file="$OUT_DIR/$name.command.txt"
  local exit_file="$OUT_DIR/$name.exitcode.txt"
  local output
  local status=0

  write_command_file "$command_file" "$@"
  set +e
  output="$("$@" 2>&1)"
  status=$?
  set -e

  printf '%s\n' "$output" >"$output_file"
  printf '%s\n' "$status" >"$exit_file"

  if [[ $status -ne 0 ]]; then
    return $status
  fi
}

run_json_step_allow_fail() {
  local name="$1"
  shift
  local output_file="$OUT_DIR/$name.json"
  local command_file="$OUT_DIR/$name.command.txt"
  local exit_file="$OUT_DIR/$name.exitcode.txt"
  local output
  local status=0

  write_command_file "$command_file" "$@"
  set +e
  output="$("$@" 2>&1)"
  status=$?
  set -e

  printf '%s\n' "$output" >"$output_file"
  printf '%s\n' "$status" >"$exit_file"
}

assert_contains() {
  local pattern="$1"
  local file="$2"
  local message="$3"
  if ! rg -q --pcre2 "$pattern" "$file"; then
    echo "ASSERTION FAILED: $message" >&2
    echo "FILE: $file" >&2
    exit 1
  fi
}

assert_file_exists() {
  local path="$1"
  local message="$2"
  if [[ ! -f "$path" ]]; then
    echo "ASSERTION FAILED: $message" >&2
    echo "MISSING FILE: $path" >&2
    exit 1
  fi
}

assert_number_gt() {
  local value="$1"
  local threshold="$2"
  local message="$3"
  if ! awk -v value="$value" -v threshold="$threshold" 'BEGIN { exit !(value > threshold) }'; then
    echo "ASSERTION FAILED: $message" >&2
    echo "VALUE: $value" >&2
    echo "THRESHOLD: $threshold" >&2
    exit 1
  fi
}

assert_files_differ() {
  local left="$1"
  local right="$2"
  local message="$3"
  if cmp -s "$left" "$right"; then
    echo "ASSERTION FAILED: $message" >&2
    echo "FILES MATCH: $left $right" >&2
    exit 1
  fi
}

assert_file_empty() {
  local file="$1"
  local message="$2"
  if [[ -s "$file" ]]; then
    echo "ASSERTION FAILED: $message" >&2
    echo "FILE: $file" >&2
    cat "$file" >&2
    exit 1
  fi
}

extract_json_string() {
  local field="$1"
  local file="$2"
  rg -o --pcre2 "\"$field\"\\s*:\\s*\"\\K(?:[^\"\\\\]|\\\\.)*(?=\")" "$file" | head -n1
}

extract_json_number() {
  local field="$1"
  local file="$2"
  rg -o --pcre2 "\"$field\"\\s*:\\s*\\K-?[0-9]+(?:\\.[0-9]+)?" "$file" | head -n1
}

collect_crash_files() {
  find "$HOME/Library/Logs/DiagnosticReports" -maxdepth 1 -type f -name 'Unity-*.ips' -print 2>/dev/null | sort
}

cleanup_fixture() {
  local cleanup_code
  cleanup_code=$(cat <<'CS'
var titles = new[]
{
    "UniCli UITree Test",
    "UniCli Mixed Capture Test",
    "UniCli Pure IMGUI Capture Test"
};

var windows = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>();
foreach (var window in windows)
{
    if (window != null && window.titleContent != null && System.Array.IndexOf(titles, window.titleContent.text) >= 0)
    {
        window.Close();
    }
}

var root = UnityEngine.GameObject.Find("UITreeVerifyRoot");
if (root != null)
{
    UnityEngine.Object.DestroyImmediate(root);
}

var materials = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Material>();
foreach (var material in materials)
{
    if (material != null && material.name != null && material.name.StartsWith("UITreeVerifyMat_"))
    {
        UnityEngine.Object.DestroyImmediate(material);
    }
}

return "cleanup";
CS
)
  run_unicli eval "$cleanup_code" --json >/dev/null 2>&1 || true
}
trap cleanup_fixture EXIT

create_image_probe() {
  cat >"$IMAGE_PROBE" <<'SWIFT'
import AppKit
import Foundation

enum ProbeError: Error, CustomStringConvertible {
    case usage(String)
    case loadFailed(String)
    case assertionFailed(String)

    var description: String {
        switch self {
        case .usage(let message): return message
        case .loadFailed(let message): return message
        case .assertionFailed(let message): return message
        }
    }
}

struct PixelSample {
    let x: Int
    let y: Int
    let r: Int
    let g: Int
    let b: Int
    let a: Int
}

func loadBitmap(_ path: String) throws -> NSBitmapImageRep {
    guard let image = NSImage(contentsOfFile: path) else {
        throw ProbeError.loadFailed("Unable to load image at \(path)")
    }

    guard let data = image.tiffRepresentation,
          let bitmap = NSBitmapImageRep(data: data) else {
        throw ProbeError.loadFailed("Unable to decode bitmap for \(path)")
    }

    return bitmap
}

func sample(_ bitmap: NSBitmapImageRep, x: Int, yFromTop: Int) -> PixelSample {
    let clampedX = max(0, min(bitmap.pixelsWide - 1, x))
    let clampedYFromTop = max(0, min(bitmap.pixelsHigh - 1, yFromTop))
    let y = bitmap.pixelsHigh - 1 - clampedYFromTop

    let color = (bitmap.colorAt(x: clampedX, y: y) ?? .clear).usingColorSpace(.deviceRGB) ?? .clear
    return PixelSample(
        x: clampedX,
        y: clampedYFromTop,
        r: Int(round(color.redComponent * 255.0)),
        g: Int(round(color.greenComponent * 255.0)),
        b: Int(round(color.blueComponent * 255.0)),
        a: Int(round(color.alphaComponent * 255.0))
    )
}

func containsDominantPixel(_ bitmap: NSBitmapImageRep, xMinRatio: Double, xMaxRatio: Double, channel: String, primaryMin: Int, delta: Int) -> PixelSample? {
    let width = bitmap.pixelsWide
    let height = bitmap.pixelsHigh
    let xMin = max(0, min(width - 1, Int(Double(width) * xMinRatio)))
    let xMax = max(xMin + 1, min(width, Int(ceil(Double(width) * xMaxRatio))))

    for yFromTop in stride(from: 0, to: height, by: 4) {
        for x in stride(from: xMin, to: xMax, by: 4) {
            let pixel = sample(bitmap, x: x, yFromTop: yFromTop)
            switch channel {
            case "red":
                if pixel.r >= primaryMin && pixel.r >= pixel.g + delta && pixel.r >= pixel.b + delta {
                    return pixel
                }
            case "green":
                if pixel.g >= primaryMin && pixel.g >= pixel.r + delta && pixel.g >= pixel.b + delta {
                    return pixel
                }
            case "blue":
                if pixel.b >= primaryMin && pixel.b >= pixel.r + delta && pixel.b >= pixel.g + delta {
                    return pixel
                }
            default:
                return nil
            }
        }
    }

    return nil
}

func run() throws {
    let args = CommandLine.arguments
    guard args.count >= 2 else {
        throw ProbeError.usage("Usage: image_probe.swift <mode> ...")
    }

    switch args[1] {
    case "dark-inset":
        guard args.count == 8 else {
            throw ProbeError.usage("Usage: image_probe.swift dark-inset <image> <x> <y> <maxR> <maxG> <maxB>")
        }
        let bitmap = try loadBitmap(args[2])
        let pixel = sample(bitmap, x: Int(args[3]) ?? 0, yFromTop: Int(args[4]) ?? 0)
        let maxR = Int(args[5]) ?? 255
        let maxG = Int(args[6]) ?? 255
        let maxB = Int(args[7]) ?? 255

        guard pixel.r <= maxR && pixel.g <= maxG && pixel.b <= maxB else {
            throw ProbeError.assertionFailed("Pixel at (\(pixel.x),\(pixel.y)) expected dark <= [\(maxR),\(maxG),\(maxB)], actual [\(pixel.r),\(pixel.g),\(pixel.b),\(pixel.a)]")
        }

        print("OK dark-inset \(pixel.r),\(pixel.g),\(pixel.b),\(pixel.a)")
    case "alpha-inset-max":
        guard args.count == 6 else {
            throw ProbeError.usage("Usage: image_probe.swift alpha-inset-max <image> <x> <y> <maxA>")
        }
        let bitmap = try loadBitmap(args[2])
        let pixel = sample(bitmap, x: Int(args[3]) ?? 0, yFromTop: Int(args[4]) ?? 0)
        let maxA = Int(args[5]) ?? 255

        guard pixel.a <= maxA else {
            throw ProbeError.assertionFailed("Pixel at (\(pixel.x),\(pixel.y)) expected alpha <= \(maxA), actual \(pixel.a)")
        }

        print("OK alpha-inset-max \(pixel.r),\(pixel.g),\(pixel.b),\(pixel.a)")
    case "contains-dominant":
        guard args.count == 8 else {
            throw ProbeError.usage("Usage: image_probe.swift contains-dominant <image> <xMinRatio> <xMaxRatio> <channel> <primaryMin> <delta>")
        }
        let bitmap = try loadBitmap(args[2])
        let xMinRatio = Double(args[3]) ?? 0
        let xMaxRatio = Double(args[4]) ?? 1
        let channel = args[5]
        let primaryMin = Int(args[6]) ?? 0
        let delta = Int(args[7]) ?? 0

        guard let pixel = containsDominantPixel(bitmap, xMinRatio: xMinRatio, xMaxRatio: xMaxRatio, channel: channel, primaryMin: primaryMin, delta: delta) else {
            throw ProbeError.assertionFailed("No \(channel)-dominant pixel found in x-range \(xMinRatio)-\(xMaxRatio)")
        }

        print("OK contains-dominant \(channel) x=\(pixel.x) y=\(pixel.y) rgba=\(pixel.r),\(pixel.g),\(pixel.b),\(pixel.a)")
    default:
        throw ProbeError.usage("Unknown mode: \(args[1])")
    }
}

do {
    try run()
} catch {
    fputs("\(error)\n", stderr)
    exit(1)
}
SWIFT
}

ensure_server_running
create_image_probe
collect_crash_files >"$OUT_DIR/00_crash_before.txt"

# 1) Connectivity and discoverability
run_text_step "01_check" run_unicli check
assert_contains 'Server:\s+running' "$OUT_DIR/01_check.log" "UniCli server should be running"

run_text_step "02_status" run_unicli status
assert_contains 'Server:\s+running' "$OUT_DIR/02_status.log" "UniCli status should report running server"

run_json_step "03_commands_json" run_unicli commands --json
assert_contains '"name"\s*:\s*"UITree\.Dump"' "$OUT_DIR/03_commands_json.json" "UITree.Dump must be discoverable"
assert_contains '"name"\s*:\s*"UITree\.Inspect"' "$OUT_DIR/03_commands_json.json" "UITree.Inspect must be discoverable"
assert_contains '"name"\s*:\s*"UITree\.Click"' "$OUT_DIR/03_commands_json.json" "UITree.Click must be discoverable"
assert_contains '"name"\s*:\s*"UITree\.Fill"' "$OUT_DIR/03_commands_json.json" "UITree.Fill must be discoverable"
assert_contains '"name"\s*:\s*"UITree\.Select"' "$OUT_DIR/03_commands_json.json" "UITree.Select must be discoverable"
assert_contains '"name"\s*:\s*"Screenshot\.CaptureEditor"' "$OUT_DIR/03_commands_json.json" "Screenshot.CaptureEditor must be discoverable"
assert_contains '"name"\s*:\s*"Screenshot\.CaptureCamera"' "$OUT_DIR/03_commands_json.json" "Screenshot.CaptureCamera must be discoverable"

for cmd in UITree.Dump UITree.Inspect UITree.Click UITree.Fill UITree.Select Screenshot.CaptureEditor Screenshot.CaptureCamera; do
  safe_name="$(echo "$cmd" | tr '.' '_')"
  run_text_step "04_help_${safe_name}" run_unicli exec "$cmd" --help
  assert_contains "^${cmd//./\\.} - " "$OUT_DIR/04_help_${safe_name}.log" "$cmd help output should be available"
done

# 2) Unity import, compile, and targeted regressions
run_json_step "05_import_handlers" run_unicli exec AssetDatabase.Import --path "Packages/com.yucchiy.unicli-server/Editor/Handlers/UITree" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/05_import_handlers.json" "Handlers import should succeed"

run_json_step "06_import_tests" run_unicli exec AssetDatabase.Import --path "Packages/com.yucchiy.unicli-server/Tests/Editor/UITree" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/06_import_tests.json" "Tests import should succeed"

run_json_step "07_compile" run_unicli exec Compile --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/07_compile.json" "Compile should succeed"
assert_contains '"errorCount"\s*:\s*0' "$OUT_DIR/07_compile.json" "Compile should have zero errors"

run_json_step "08_test_editor_capture" run_unicli exec TestRunner.RunEditMode --assemblies "UniCli.Server.Editor.Tests" --testNames "UniCli.Server.Editor.Tests.ScreenshotCaptureEditorHandlerTests.Execute_CapturePanelAndDiff_WritesImages" --resultFilter none --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/08_test_editor_capture.json" "Editor screenshot regression test should pass"
assert_contains '"failed"\s*:\s*0' "$OUT_DIR/08_test_editor_capture.json" "Editor screenshot regression test should have zero failures"

run_json_step "09_test_mixed_capture" run_unicli exec TestRunner.RunEditMode --assemblies "UniCli.Server.Editor.Tests" --testNames "UniCli.Server.Editor.Tests.ScreenshotCaptureEditorMixedWindowTests.Execute_CaptureMixedWindow_IncludesImguiAndUitkContent" --resultFilter none --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/09_test_mixed_capture.json" "Mixed window regression test should pass"
assert_contains '"failed"\s*:\s*0' "$OUT_DIR/09_test_mixed_capture.json" "Mixed window regression test should have zero failures"

run_json_step "10_test_pure_imgui_capture" run_unicli exec TestRunner.RunEditMode --assemblies "UniCli.Server.Editor.Tests" --testNames "UniCli.Server.Editor.Tests.ScreenshotCaptureEditorPureImguiWindowTests.Execute_CapturePureImguiWindow_IncludesLegacyContent" --resultFilter none --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/10_test_pure_imgui_capture.json" "Pure IMGUI regression test should pass"
assert_contains '"failed"\s*:\s*0' "$OUT_DIR/10_test_pure_imgui_capture.json" "Pure IMGUI regression test should have zero failures"

run_json_step "11_test_camera_capture" run_unicli exec TestRunner.RunEditMode --assemblies "UniCli.Server.Editor.Tests" --testNames "UniCli.Server.Editor.Tests.ScreenshotCaptureCameraHandlerTests.Execute_CapturesCameraImage" --resultFilter none --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/11_test_camera_capture.json" "Camera screenshot regression test should pass"
assert_contains '"failed"\s*:\s*0' "$OUT_DIR/11_test_camera_capture.json" "Camera screenshot regression test should have zero failures"

run_json_step "12_editmode_tests" run_unicli exec TestRunner.RunEditMode --resultFilter none --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/12_editmode_tests.json" "EditMode tests should succeed"
assert_contains '"failed"\s*:\s*0' "$OUT_DIR/12_editmode_tests.json" "EditMode tests should have zero failures"

# 3) Functional E2E validation for all commands and capture modes
cleanup_fixture

setup_code=$(cat <<'CS'
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
        throw new System.InvalidOperationException("Fixture type not found: " + fullName);
    }

    var openMethod = type.GetMethod("Open", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
    if (openMethod == null)
    {
        throw new System.InvalidOperationException("Open() not found on fixture type: " + fullName);
    }

    return (UnityEditor.EditorWindow)openMethod.Invoke(null, null);
}

UnityEngine.Material CreateMaterial(string name, UnityEngine.Color color)
{
    var shader = UnityEngine.Shader.Find("Universal Render Pipeline/Lit") ?? UnityEngine.Shader.Find("Standard");
    var material = new UnityEngine.Material(shader);
    material.name = name;
    material.color = color;
    return material;
}

var uitreeWindow = UniCli.Server.Editor.Tests.UITreeTestEditorWindow.Open();
uitreeWindow.Focus();
uitreeWindow.Repaint();

var mixedWindow = OpenFixture("UniCli.Server.Editor.Tests.MixedCaptureTestEditorWindow");
mixedWindow.Focus();
mixedWindow.Repaint();

var pureImguiWindow = OpenFixture("UniCli.Server.Editor.Tests.PureImguiCaptureTestEditorWindow");
pureImguiWindow.Focus();
pureImguiWindow.Repaint();

var oldRoot = UnityEngine.GameObject.Find("UITreeVerifyRoot");
if (oldRoot != null)
{
    UnityEngine.Object.DestroyImmediate(oldRoot);
}

var root = new UnityEngine.GameObject("UITreeVerifyRoot");

var cameraGo = new UnityEngine.GameObject("UITreeVerifyCamera");
cameraGo.transform.SetParent(root.transform);
var cam = cameraGo.AddComponent<UnityEngine.Camera>();
cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
cam.backgroundColor = new UnityEngine.Color(0.16f, 0.17f, 0.24f);
cam.transform.position = new UnityEngine.Vector3(0f, 1.8f, -5.5f);
cam.transform.LookAt(new UnityEngine.Vector3(0f, 0.9f, 0f));

var lightGo = new UnityEngine.GameObject("UITreeVerifyLight");
lightGo.transform.SetParent(root.transform);
var light = lightGo.AddComponent<UnityEngine.Light>();
light.type = UnityEngine.LightType.Directional;
light.intensity = 1.2f;
light.transform.rotation = UnityEngine.Quaternion.Euler(40f, -35f, 0f);

var plane = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Plane);
plane.name = "UITreeVerifyPlane";
plane.transform.SetParent(root.transform);
plane.transform.localScale = new UnityEngine.Vector3(0.5f, 1f, 0.5f);
plane.GetComponent<UnityEngine.Renderer>().sharedMaterial = CreateMaterial("UITreeVerifyMat_Plane", new UnityEngine.Color(0.28f, 0.30f, 0.34f));

var cube = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
cube.name = "UITreeVerifyCube";
cube.transform.SetParent(root.transform);
cube.transform.position = new UnityEngine.Vector3(-1.2f, 0.6f, 0f);
cube.GetComponent<UnityEngine.Renderer>().sharedMaterial = CreateMaterial("UITreeVerifyMat_Cube", new UnityEngine.Color(0.91f, 0.33f, 0.22f));

var sphere = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere);
sphere.name = "UITreeVerifySphere";
sphere.transform.SetParent(root.transform);
sphere.transform.position = new UnityEngine.Vector3(1.1f, 0.75f, 0.4f);
sphere.GetComponent<UnityEngine.Renderer>().sharedMaterial = CreateMaterial("UITreeVerifyMat_Sphere", new UnityEngine.Color(0.23f, 0.58f, 0.93f));

UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
return "fixture-ready";
CS
)

run_json_step "13_setup_fixture" run_unicli eval "$setup_code" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/13_setup_fixture.json" "Fixture setup should succeed"

run_json_step "14_dump_panel" run_unicli exec UITree.Dump --panel "UniCli UITree Test" --depth 10 --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/14_dump_panel.json" "UITree.Dump should succeed"
assert_contains '"panelInfo"' "$OUT_DIR/14_dump_panel.json" "UITree.Dump should include panelInfo"
assert_contains '"name"\s*:\s*"UniCli\.Server\.Editor\.Tests\.UITreeTestEditorWindow"' "$OUT_DIR/14_dump_panel.json" "UITree.Dump should resolve the fixture window"
assert_contains 'preview-swatch' "$OUT_DIR/14_dump_panel.json" "UITree.Dump should include the preview swatch"

run_json_step "15_dump_filter" run_unicli exec UITree.Dump --panel "UniCli UITree Test" --filter ".editable" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/15_dump_filter.json" "Filtered UITree.Dump should succeed"
assert_contains '"matchedCount"\s*:\s*2' "$OUT_DIR/15_dump_filter.json" "Filtered UITree.Dump should report two editable fields"
assert_contains 'name-field' "$OUT_DIR/15_dump_filter.json" "Filtered UITree.Dump should include name-field"
assert_contains 'count-field' "$OUT_DIR/15_dump_filter.json" "Filtered UITree.Dump should include count-field"

run_json_step "16_dump_list" run_unicli exec UITree.Dump --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/16_dump_list.json" "UITree.Dump summary should succeed"

run_json_step "17_inspect_name_field" run_unicli exec UITree.Inspect --panel "UniCli UITree Test" --selector "#name-field" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/17_inspect_name_field.json" "UITree.Inspect should succeed"
assert_contains '"bindingPath"\s*:\s*"fixture\.name"' "$OUT_DIR/17_inspect_name_field.json" "UITree.Inspect should include the binding path"
assert_contains '"type"\s*:\s*"UnityEngine\.UIElements\.TextField"' "$OUT_DIR/17_inspect_name_field.json" "UITree.Inspect should resolve the TextField"

EDITOR_BEFORE="$OUT_DIR/editor_before.png"
EDITOR_AFTER="$OUT_DIR/editor_after.png"
EDITOR_CROP="$OUT_DIR/editor_crop.png"
EDITOR_DIFF="$OUT_DIR/editor_after_diff.png"
MIXED_CAPTURE="$OUT_DIR/mixed_window.png"
PURE_IMGUI_CAPTURE="$OUT_DIR/pure_imgui_window.png"
CAMERA_BEFORE="$OUT_DIR/camera_before.png"
CAMERA_AFTER="$OUT_DIR/camera_after.png"
CAMERA_TRANSPARENT="$OUT_DIR/camera_transparent.png"

run_json_step "18_capture_editor_before" run_unicli exec Screenshot.CaptureEditor --panel "UniCli UITree Test" --filename "$EDITOR_BEFORE" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/18_capture_editor_before.json" "Initial editor capture should succeed"
assert_file_exists "$EDITOR_BEFORE" "Initial editor screenshot should exist"
run_text_step "19_probe_editor_before_dark" swift "$IMAGE_PROBE" dark-inset "$EDITOR_BEFORE" 20 20 90 90 102

run_json_step "20_click" run_unicli exec UITree.Click --panel "UniCli UITree Test" --selector "#run-button" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/20_click.json" "UITree.Click should succeed"
assert_contains '"result"\s*:\s*"success"' "$OUT_DIR/20_click.json" "UITree.Click result should be success"
assert_contains 'clicked:1' "$OUT_DIR/20_click.json" "UITree.Click should report the updated status"

run_json_step "21_inspect_status_after_click" run_unicli exec UITree.Inspect --panel "UniCli UITree Test" --selector "#status-label" --json
assert_contains 'clicked:1' "$OUT_DIR/21_inspect_status_after_click.json" "Status label should reflect the click"

run_json_step "22_fill_text" run_unicli exec UITree.Fill --panel "UniCli UITree Test" --selector "#name-field" --value "Verification Title" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/22_fill_text.json" "UITree.Fill text should succeed"
assert_contains '"newValue"\s*:\s*"Verification Title"' "$OUT_DIR/22_fill_text.json" "UITree.Fill should set the text value"
assert_contains 'filled:Verification Title' "$OUT_DIR/22_fill_text.json" "UITree.Fill should record status side effects"

run_json_step "23_fill_number" run_unicli exec UITree.Fill --panel "UniCli UITree Test" --selector "#count-field" --value "42" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/23_fill_number.json" "UITree.Fill number should succeed"
assert_contains '"newValue"\s*:\s*"42"' "$OUT_DIR/23_fill_number.json" "UITree.Fill should set the numeric value"

run_json_step "24_inspect_preview_title" run_unicli exec UITree.Inspect --panel "UniCli UITree Test" --selector "#preview-title" --json
assert_contains 'Verification Title' "$OUT_DIR/24_inspect_preview_title.json" "Preview title should reflect the filled text"

run_json_step "25_select_mode" run_unicli exec UITree.Select --panel "UniCli UITree Test" --selector "#mode-dropdown" --choice "Gamma" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/25_select_mode.json" "UITree.Select should succeed"
assert_contains '"newValue"\s*:\s*"Gamma"' "$OUT_DIR/25_select_mode.json" "UITree.Select should set Gamma"
assert_contains '"choices"' "$OUT_DIR/25_select_mode.json" "UITree.Select should return choices"

run_json_step "26_inspect_status_after_select" run_unicli exec UITree.Inspect --panel "UniCli UITree Test" --selector "#status-label" --json
assert_contains 'mode:Gamma' "$OUT_DIR/26_inspect_status_after_select.json" "Status label should reflect Gamma selection"

run_json_step "27_capture_editor_after" run_unicli exec Screenshot.CaptureEditor --panel "UniCli UITree Test" --filename "$EDITOR_AFTER" --diffBase "$EDITOR_BEFORE" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/27_capture_editor_after.json" "Editor diff capture should succeed"
assert_file_exists "$EDITOR_AFTER" "Changed editor screenshot should exist"
EDITOR_DIFF="$(extract_json_string diffImagePath "$OUT_DIR/27_capture_editor_after.json")"
assert_file_exists "$EDITOR_DIFF" "Diff image should exist"
assert_contains '"changedPixels"\s*:\s*[1-9][0-9]*' "$OUT_DIR/27_capture_editor_after.json" "Editor diff should report changed pixels"
assert_files_differ "$EDITOR_BEFORE" "$EDITOR_AFTER" "Editor screenshots should differ after interaction commands"

run_json_step "28_capture_editor_crop" run_unicli exec Screenshot.CaptureEditor --panel "UniCli UITree Test" --selector "#preview-swatch" --filename "$EDITOR_CROP" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/28_capture_editor_crop.json" "Editor crop capture should succeed"
assert_file_exists "$EDITOR_CROP" "Editor crop screenshot should exist"
assert_number_gt "$(extract_json_number width "$OUT_DIR/28_capture_editor_crop.json")" 100 "Editor crop width should be meaningful"
assert_number_gt "$(extract_json_number height "$OUT_DIR/28_capture_editor_crop.json")" 60 "Editor crop height should be meaningful"
run_text_step "29_probe_editor_crop_green" swift "$IMAGE_PROBE" contains-dominant "$EDITOR_CROP" 0 1 green 115 15

run_json_step "30_capture_mixed_window" run_unicli exec Screenshot.CaptureEditor --panel "UniCli Mixed Capture Test" --filename "$MIXED_CAPTURE" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/30_capture_mixed_window.json" "Mixed window capture should succeed"
assert_file_exists "$MIXED_CAPTURE" "Mixed window capture should exist"
run_text_step "31_probe_mixed_red" swift "$IMAGE_PROBE" contains-dominant "$MIXED_CAPTURE" 0 0.5 red 140 20
run_text_step "32_probe_mixed_green" swift "$IMAGE_PROBE" contains-dominant "$MIXED_CAPTURE" 0.5 1 green 120 20

run_json_step "33_capture_pure_imgui" run_unicli exec Screenshot.CaptureEditor --panel "UniCli Pure IMGUI Capture Test" --filename "$PURE_IMGUI_CAPTURE" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/33_capture_pure_imgui.json" "Pure IMGUI capture should succeed"
assert_file_exists "$PURE_IMGUI_CAPTURE" "Pure IMGUI capture should exist"
run_text_step "34_probe_pure_imgui_red" swift "$IMAGE_PROBE" contains-dominant "$PURE_IMGUI_CAPTURE" 0 0.6 red 140 20

run_json_step "35_capture_camera_before" run_unicli exec Screenshot.CaptureCamera --cameraName "UITreeVerifyCamera" --width 640 --height 360 --filename "$CAMERA_BEFORE" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/35_capture_camera_before.json" "Camera capture should succeed"
assert_file_exists "$CAMERA_BEFORE" "Camera capture should exist"
run_text_step "36_probe_camera_dark" swift "$IMAGE_PROBE" dark-inset "$CAMERA_BEFORE" 10 10 80 90 110
run_text_step "37_probe_camera_red" swift "$IMAGE_PROBE" contains-dominant "$CAMERA_BEFORE" 0 0.5 red 110 25
run_text_step "38_probe_camera_blue" swift "$IMAGE_PROBE" contains-dominant "$CAMERA_BEFORE" 0.5 1 blue 110 25

mutate_scene_code=$(cat <<'CS'
var cube = UnityEngine.GameObject.Find("UITreeVerifyCube");
if (cube != null)
{
    cube.transform.position = new UnityEngine.Vector3(-0.1f, 1.4f, -0.2f);
}

var sphere = UnityEngine.GameObject.Find("UITreeVerifySphere");
if (sphere != null)
{
    sphere.GetComponent<UnityEngine.Renderer>().sharedMaterial.color = new UnityEngine.Color(0.95f, 0.78f, 0.25f);
}

UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
return "scene-mutated";
CS
)

run_json_step "39_mutate_scene" run_unicli eval "$mutate_scene_code" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/39_mutate_scene.json" "Scene mutation should succeed"

run_json_step "40_capture_camera_after" run_unicli exec Screenshot.CaptureCamera --cameraName "UITreeVerifyCamera" --width 640 --height 360 --filename "$CAMERA_AFTER" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/40_capture_camera_after.json" "Second camera capture should succeed"
assert_file_exists "$CAMERA_AFTER" "Second camera capture should exist"
assert_files_differ "$CAMERA_BEFORE" "$CAMERA_AFTER" "Camera capture should change after scene mutation"

run_json_step "41_capture_camera_transparent" run_unicli exec Screenshot.CaptureCamera --cameraName "UITreeVerifyCamera" --width 640 --height 360 --filename "$CAMERA_TRANSPARENT" --transparent --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/41_capture_camera_transparent.json" "Transparent camera capture should succeed"
assert_file_exists "$CAMERA_TRANSPARENT" "Transparent camera capture should exist"
run_text_step "42_probe_camera_transparent_alpha" swift "$IMAGE_PROBE" alpha-inset-max "$CAMERA_TRANSPARENT" 5 5 10

# 4) Negative path checks
run_json_step_allow_fail "43_inspect_no_match" run_unicli exec UITree.Inspect --panel "UniCli UITree Test" --selector "#does-not-exist" --json
assert_contains '"success"\s*:\s*false' "$OUT_DIR/43_inspect_no_match.json" "UITree.Inspect no-match should fail"
assert_contains 'No element matched selector' "$OUT_DIR/43_inspect_no_match.json" "UITree.Inspect no-match should return a clear error"

run_json_step_allow_fail "44_select_invalid_choice" run_unicli exec UITree.Select --panel "UniCli UITree Test" --selector "#mode-dropdown" --choice "Nope" --json
assert_contains '"success"\s*:\s*false' "$OUT_DIR/44_select_invalid_choice.json" "UITree.Select invalid choice should fail"
assert_contains 'does not exist' "$OUT_DIR/44_select_invalid_choice.json" "UITree.Select invalid choice should return a clear error"

run_json_step_allow_fail "45_camera_not_found" run_unicli exec Screenshot.CaptureCamera --cameraName "NoSuchCamera" --json
assert_contains '"success"\s*:\s*false' "$OUT_DIR/45_camera_not_found.json" "Screenshot.CaptureCamera missing camera should fail"
assert_contains 'not found' "$OUT_DIR/45_camera_not_found.json" "Screenshot.CaptureCamera missing camera should return a clear error"

# 5) Cleanup and crash audit
run_json_step "46_cleanup_fixture" run_unicli eval "
var titles = new[]
{
    \"UniCli UITree Test\",
    \"UniCli Mixed Capture Test\",
    \"UniCli Pure IMGUI Capture Test\"
};

var windows = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>();
foreach (var window in windows)
{
    if (window != null && window.titleContent != null && System.Array.IndexOf(titles, window.titleContent.text) >= 0)
    {
        window.Close();
    }
}

var root = UnityEngine.GameObject.Find(\"UITreeVerifyRoot\");
if (root != null)
{
    UnityEngine.Object.DestroyImmediate(root);
}

var materials = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.Material>();
foreach (var material in materials)
{
    if (material != null && material.name != null && material.name.StartsWith(\"UITreeVerifyMat_\"))
    {
        UnityEngine.Object.DestroyImmediate(material);
    }
}

return \"cleanup\";
" --json
assert_contains '"success"\s*:\s*true' "$OUT_DIR/46_cleanup_fixture.json" "Cleanup should succeed"

collect_crash_files >"$OUT_DIR/47_crash_after.txt"
comm -13 "$OUT_DIR/00_crash_before.txt" "$OUT_DIR/47_crash_after.txt" >"$OUT_DIR/48_new_crashes.txt"
assert_file_empty "$OUT_DIR/48_new_crashes.txt" "No new Unity crash reports should be generated during verification"

PASSED_TESTS="$(extract_json_number passed "$OUT_DIR/12_editmode_tests.json")"
TOTAL_TESTS="$(extract_json_number total "$OUT_DIR/12_editmode_tests.json")"
EDITOR_CHANGED_PIXELS="$(extract_json_number changedPixels "$OUT_DIR/27_capture_editor_after.json")"
EDITOR_CHANGED_PERCENT="$(extract_json_number percentage "$OUT_DIR/27_capture_editor_after.json")"
FILTER_MATCHED="$(extract_json_number matchedCount "$OUT_DIR/15_dump_filter.json")"
CAMERA_WIDTH="$(extract_json_number width "$OUT_DIR/35_capture_camera_before.json")"
CAMERA_HEIGHT="$(extract_json_number height "$OUT_DIR/35_capture_camera_before.json")"

REPORT="$OUT_DIR/REPORT.md"
cat <<REPORT_EOF >"$REPORT"
# UITree Full Verification Report

- Timestamp: $TIMESTAMP
- Project: $PROJECT_DIR
- Output directory: $OUT_DIR
- Result: PASS

## Validation scope

1. UniCli connectivity and discoverability for all 7 custom commands
2. Unity asset import and compile gates
3. Focused screenshot regression tests
4. Full EditMode suite
5. Functional command execution for UITree and Screenshot flows
6. Visual assertions against generated PNGs
7. Crash audit against macOS Unity diagnostic reports

## Summary

- EditMode tests: $PASSED_TESTS / $TOTAL_TESTS passed
- Filtered dump matched count: $FILTER_MATCHED
- Editor diff changed pixels: $EDITOR_CHANGED_PIXELS
- Editor diff changed percent: $EDITOR_CHANGED_PERCENT
- Camera capture resolution: ${CAMERA_WIDTH}x${CAMERA_HEIGHT}
- Crash reports generated during run: 0

## Key command outputs

- [14_dump_panel.json]($OUT_DIR/14_dump_panel.json)
- [15_dump_filter.json]($OUT_DIR/15_dump_filter.json)
- [17_inspect_name_field.json]($OUT_DIR/17_inspect_name_field.json)
- [20_click.json]($OUT_DIR/20_click.json)
- [22_fill_text.json]($OUT_DIR/22_fill_text.json)
- [23_fill_number.json]($OUT_DIR/23_fill_number.json)
- [25_select_mode.json]($OUT_DIR/25_select_mode.json)
- [27_capture_editor_after.json]($OUT_DIR/27_capture_editor_after.json)
- [35_capture_camera_before.json]($OUT_DIR/35_capture_camera_before.json)
- [41_capture_camera_transparent.json]($OUT_DIR/41_capture_camera_transparent.json)

## Visual artifacts

- [editor_before.png]($EDITOR_BEFORE)
- [editor_after.png]($EDITOR_AFTER)
- [editor_after_diff.png]($EDITOR_DIFF)
- [editor_crop.png]($EDITOR_CROP)
- [mixed_window.png]($MIXED_CAPTURE)
- [pure_imgui_window.png]($PURE_IMGUI_CAPTURE)
- [camera_before.png]($CAMERA_BEFORE)
- [camera_after.png]($CAMERA_AFTER)
- [camera_transparent.png]($CAMERA_TRANSPARENT)

## Pixel probes

- [19_probe_editor_before_dark.log]($OUT_DIR/19_probe_editor_before_dark.log)
- [29_probe_editor_crop_green.log]($OUT_DIR/29_probe_editor_crop_green.log)
- [31_probe_mixed_red.log]($OUT_DIR/31_probe_mixed_red.log)
- [32_probe_mixed_green.log]($OUT_DIR/32_probe_mixed_green.log)
- [34_probe_pure_imgui_red.log]($OUT_DIR/34_probe_pure_imgui_red.log)
- [36_probe_camera_dark.log]($OUT_DIR/36_probe_camera_dark.log)
- [37_probe_camera_red.log]($OUT_DIR/37_probe_camera_red.log)
- [38_probe_camera_blue.log]($OUT_DIR/38_probe_camera_blue.log)
- [42_probe_camera_transparent_alpha.log]($OUT_DIR/42_probe_camera_transparent_alpha.log)

## Crash audit

- [00_crash_before.txt]($OUT_DIR/00_crash_before.txt)
- [47_crash_after.txt]($OUT_DIR/47_crash_after.txt)
- [48_new_crashes.txt]($OUT_DIR/48_new_crashes.txt)

## Negative-path checks

- [43_inspect_no_match.json]($OUT_DIR/43_inspect_no_match.json)
- [44_select_invalid_choice.json]($OUT_DIR/44_select_invalid_choice.json)
- [45_camera_not_found.json]($OUT_DIR/45_camera_not_found.json)
REPORT_EOF

echo "VERIFY_OK"
echo "OUT_DIR=$OUT_DIR"
echo "REPORT=$REPORT"
