using System.IO;
using UnityEditor;
using UnityEngine;

namespace HD2DTest.EditorTools
{
    /// <summary>
    /// Lets external tools drive the editor through flag files:
    ///  Assets/HD2DTest/build.request   → rebuild the demo scene
    ///  Assets/HD2DTest/capture.request → enter Play, wait, save Game view to HD2DTest/Captures/shot.png, exit Play
    /// </summary>
    [InitializeOnLoad]
    static class HD2DBuildWatcher
    {
        const string BuildFlag = "Assets/HD2DTest/build.request";
        const string CaptureFlag = "Assets/HD2DTest/capture.request";
        const string ShotPath = "Assets/HD2DTest/Captures/shot.png";
        const string StateKey = "HD2D_CaptureState";
        const string TimeKey = "HD2D_CaptureTime";
        static double _next;

        const string RenderFlag = "Assets/HD2DTest/render.request";
        const string StillPath = "Assets/HD2DTest/Captures/still.png";

        static HD2DBuildWatcher() => EditorApplication.update += Poll;

        /// <summary>Edit-mode render of Camera.main (with post-processing) to a PNG.</summary>
        static void RenderStill()
        {
            var cam = Camera.main;
            if (cam == null) { Debug.LogWarning("[HD2D] No main camera"); return; }
            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var req = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req))
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory(Path.GetDirectoryName(StillPath));
            File.WriteAllBytes(StillPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release(); Object.DestroyImmediate(rt);
            Debug.Log("[HD2D] Still saved: " + StillPath);
        }

        static void Consume(string flag)
        {
            File.Delete(flag);
            if (File.Exists(flag + ".meta")) File.Delete(flag + ".meta");
        }

        static void Poll()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _next) return;
            _next = now + 0.5;
            if (EditorApplication.isCompiling) return;

            int state = SessionState.GetInt(StateKey, 0);
            if (state == 1 && EditorApplication.isPlaying)
            {
                SessionState.SetInt(StateKey, 2);
                SessionState.SetFloat(TimeKey, (float)now);
                return;
            }
            if (state == 2 && EditorApplication.isPlaying && now - SessionState.GetFloat(TimeKey, 0) > 3.5)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ShotPath));
                ScreenCapture.CaptureScreenshot(ShotPath);
                SessionState.SetInt(StateKey, 3);
                SessionState.SetFloat(TimeKey, (float)now);
                return;
            }
            if (state == 3 && now - SessionState.GetFloat(TimeKey, 0) > 1.5)
            {
                SessionState.SetInt(StateKey, 0);
                EditorApplication.isPlaying = false;
                Debug.Log("[HD2D] Capture saved: " + ShotPath);
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (File.Exists(BuildFlag))
            {
                Consume(BuildFlag);
                try { HD2DSceneBuilder.Build(); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            if (File.Exists(RenderFlag))
            {
                Consume(RenderFlag);
                try { RenderStill(); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            if (File.Exists(CaptureFlag))
            {
                Consume(CaptureFlag);
                if (File.Exists(ShotPath)) File.Delete(ShotPath);
                SessionState.SetInt(StateKey, 1);
                EditorApplication.isPlaying = true;
            }
        }
    }
}
