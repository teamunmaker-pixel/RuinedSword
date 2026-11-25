using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HD2DTest.EditorTools
{
    /// <summary>
    /// Menu: HD2D Test > Build Demo Scene
    /// Builds a small HD-2D diorama: voxel terrain with pixel textures, billboard sprites
    /// that cast shadows, warm sun + lantern lights, fog, bloom, bokeh DoF, vignette.
    /// </summary>
    public static class HD2DSceneBuilder
    {
        const string Root = "Assets/HD2DTest";
        const string TexDir = Root + "/Textures";
        const string GenDir = Root + "/Generated";
        const string ScenePath = Root + "/HD2D_Demo.unity";
        const float Cell = 2f;   // world units per map cell (16px tile repeated 2x2)
        const float PPU = 16f;   // pixels per unit for every sprite & tile

        // ~ water, . grass(h1), , path(h1), 2 grass(h2), 3 grass(h3)
        static readonly string[] Map =
        {
            "333322222........~~~~~~~",
            "33332222.........~~~~~~~",
            "3332222.....,,...~~~~~~~",
            "332222.....,,,,...~~~~~~",
            "22222.....,,..,,...~~~~~",
            "2222......,....,,...~~~~",
            "222......,,.....,,..~~~~",
            "22.......,.......,...~~~",
            "2........,.......,,...~~",
            "........,,........,....~",
            "........,..........,....",
            ".......,,..........,,...",
            "......,,............,...",
            ".....,,.............,,..",
            "....,,...............,..",
            "...,,......22222.....,..",
            "..,,......2222222.....,.",
            ".,,......222222222....,,",
        };

        static int Rows => Map.Length;
        static int Cols => Map[0].Length;

        static char At(int x, int z) => Map[Rows - 1 - z][x];

        static int HeightAt(int x, int z)
        {
            if (x < 0 || z < 0 || x >= Cols || z >= Rows) return -1;
            switch (At(x, z))
            {
                case '~': return 0;
                case '2': return 2;
                case '3': return 3;
                default: return 1;
            }
        }

        [MenuItem("HD2D Test/Build Demo Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder(Root, "Generated");

            ConfigureTextures();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var quad = CreateBillboardQuad();
            var terrain = BuildTerrain();
            BuildWater();
            var player = BuildPlayer(quad);
            BuildDecorations(quad);
            BuildLighting();
            var volume = BuildVolume();
            BuildCamera(player.transform, volume);
            BuildMotes();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("[HD2D] Demo scene built: " + ScenePath + "  (Play → WASD / arrow keys)");
        }

        // ---------------------------------------------------------------- textures / materials
        static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                bool tile = path.Contains("grass_top") || path.Contains("dirt_") || path.Contains("path_top")
                            || path.Contains("riverbed") || path.Contains("water");
                ti.textureType = TextureImporterType.Default;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.filterMode = FilterMode.Point;
                ti.mipmapEnabled = false;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.alphaIsTransparency = !tile;
                ti.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                ti.sRGBTexture = true;
                ti.SaveAndReimport();
            }
        }

        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");

        static T SaveAsset<T>(T obj, string name) where T : Object
        {
            string path = $"{GenDir}/{name}";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        static Material LitMaterial(string name, Texture2D tex, bool cutout, float smoothness = 0f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", smoothness);
            if (smoothness < 0.5f)
            {
                m.SetFloat("_SpecularHighlights", 0f);
                m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_EnvironmentReflections", 0f);
                m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            if (cutout)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f); // both faces
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            return SaveAsset(m, name + ".mat");
        }

        // ---------------------------------------------------------------- meshes
        static void AddQuad(List<Vector3> v, List<Vector2> uv, List<int> tris,
                            Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 normal)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0)
            { tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i); tris.Add(i + 2); tris.Add(i + 3); }
            else
            { tris.Add(i); tris.Add(i + 2); tris.Add(i + 1); tris.Add(i); tris.Add(i + 3); tris.Add(i + 2); }
        }

        static Mesh CreateBillboardQuad()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            AddQuad(v, uv, t,
                new Vector3(-0.5f, 0, 0), new Vector3(-0.5f, 1, 0), new Vector3(0.5f, 1, 0), new Vector3(0.5f, 0, 0),
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), Vector3.back);
            var mesh = new Mesh { name = "HD2D_BillboardQuad" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return SaveAsset(mesh, "HD2D_BillboardQuad.asset");
        }

        static GameObject BuildTerrain()
        {
            // submeshes: 0 grass top, 1 path top, 2 riverbed top, 3 cliff (grass lip), 4 cliff (deep)
            var v = new List<Vector3>(); var uv = new List<Vector2>();
            var sub = new List<int>[5];
            for (int i = 0; i < sub.Length; i++) sub[i] = new List<int>();
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

            for (int z = 0; z < Rows; z++)
            for (int x = 0; x < Cols; x++)
            {
                int h = HeightAt(x, z);
                float x0 = x * Cell, x1 = x0 + Cell, z0 = z * Cell, z1 = z0 + Cell;
                char c = At(x, z);
                int topSub = c == ',' ? 1 : c == '~' ? 2 : 0;
                AddQuad(v, uv, sub[topSub],
                    new Vector3(x0, h, z0), new Vector3(x0, h, z1), new Vector3(x1, h, z1), new Vector3(x1, h, z0),
                    new Vector2(0, 0), new Vector2(0, 2), new Vector2(2, 2), new Vector2(2, 0), Vector3.up);

                foreach (var d in dirs)
                {
                    int nh = HeightAt(x + d.x, z + d.y);
                    if (nh >= h) continue;
                    int bottom = Mathf.Max(nh, -1);
                    for (int y = bottom; y < h; y++)
                    {
                        int s = (y == h - 1 && c != '~') ? 3 : 4;
                        Vector3 n = new Vector3(d.x, 0, d.y);
                        if (d.x != 0)
                        {
                            float px = d.x > 0 ? x1 : x0;
                            AddQuad(v, uv, sub[s],
                                new Vector3(px, y, z0), new Vector3(px, y + 1, z0), new Vector3(px, y + 1, z1), new Vector3(px, y, z1),
                                new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 1), new Vector2(2, 0), n);
                        }
                        else
                        {
                            float pz = d.y > 0 ? z1 : z0;
                            AddQuad(v, uv, sub[s],
                                new Vector3(x0, y, pz), new Vector3(x0, y + 1, pz), new Vector3(x1, y + 1, pz), new Vector3(x1, y, pz),
                                new Vector2(0, 0), new Vector2(0, 1), new Vector2(2, 1), new Vector2(2, 0), n);
                        }
                    }
                }
            }

            var mesh = new Mesh { name = "HD2D_Terrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v); mesh.SetUVs(0, uv);
            mesh.subMeshCount = sub.Length;
            for (int i = 0; i < sub.Length; i++) mesh.SetTriangles(sub[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            SaveAsset(mesh, "HD2D_Terrain.asset");

            var go = new GameObject("Terrain");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[]
            {
                LitMaterial("M_GrassTop", Tex("grass_top"), false),
                LitMaterial("M_PathTop", Tex("path_top"), false),
                LitMaterial("M_Riverbed", Tex("riverbed"), false),
                LitMaterial("M_CliffLip", Tex("dirt_side"), false),
                LitMaterial("M_CliffDeep", Tex("dirt_plain"), false),
            };
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.isStatic = true;
            return go;
        }

        static void BuildWater()
        {
            int minX = int.MaxValue, minZ = int.MaxValue, maxX = -1, maxZ = -1;
            for (int z = 0; z < Rows; z++)
            for (int x = 0; x < Cols; x++)
                if (At(x, z) == '~') { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z); }
            if (maxX < 0) return;

            float w = (maxX - minX + 1) * Cell, d = (maxZ - minZ + 1) * Cell;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            AddQuad(v, uv, t,
                new Vector3(0, 0, 0), new Vector3(0, 0, d), new Vector3(w, 0, d), new Vector3(w, 0, 0),
                new Vector2(0, 0), new Vector2(0, d), new Vector2(w, d), new Vector2(w, 0), Vector3.up);
            var mesh = new Mesh { name = "HD2D_Water" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            SaveAsset(mesh, "HD2D_Water.asset");

            var mat = LitMaterial("M_Water", Tex("water"), false, 0.85f);
            mat.SetColor("_BaseColor", new Color(0.85f, 0.95f, 1f));
            mat.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f));

            var go = new GameObject("Water");
            go.transform.position = new Vector3(minX * Cell, 0.65f, minZ * Cell);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<HD2DUVScroll>();
        }

        // ---------------------------------------------------------------- sprites
        static GameObject Billboard(string name, Mesh quad, Material mat, int pxW, int pxH, Vector3 pos, Transform parent, float footInsetPx = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos + Vector3.down * (footInsetPx / PPU);
            go.transform.localScale = new Vector3(pxW / PPU, pxH / PPU, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.TwoSided;
            mr.receiveShadows = true;
            return go;
        }

        static Vector3 CellCenter(int x, int z) => new Vector3((x + 0.5f) * Cell, HeightAt(x, z), (z + 0.5f) * Cell);

        static GameObject BuildPlayer(Mesh quad)
        {
            // start on the first path tile found near the map centre
            Vector2Int start = new Vector2Int(Cols / 2, Rows / 2);
            float best = float.MaxValue;
            for (int z = 0; z < Rows; z++)
            for (int x = 0; x < Cols; x++)
                if (At(x, z) == ',')
                {
                    float dd = (new Vector2(x, z) - new Vector2(Cols * 0.45f, Rows * 0.55f)).sqrMagnitude;
                    if (dd < best) { best = dd; start = new Vector2Int(x, z); }
                }

            var player = new GameObject("Player");
            player.transform.position = CellCenter(start.x, start.y);
            player.AddComponent<HD2DPlayerController>();

            var mat = LitMaterial("M_Hero", Tex("hero_sheet"), true);
            mat.SetTextureScale("_BaseMap", new Vector2(1f / 3f, 1f / 4f));
            mat.SetTextureOffset("_BaseMap", new Vector2(1f / 3f, 3f / 4f));

            var sprite = Billboard("Sprite", quad, mat, 32, 48, player.transform.position, player.transform, 4f);
            sprite.AddComponent<HD2DSheetAnimator>();
            return player;
        }

        static void BuildDecorations(Mesh quad)
        {
            var root = new GameObject("Decorations").transform;
            var rnd = new System.Random(42);
            float R() => (float)rnd.NextDouble();

            var mTree = LitMaterial("M_Tree", Tex("tree"), true);
            var mBush = LitMaterial("M_Bush", Tex("bush"), true);
            var mTuft = LitMaterial("M_Tuft", Tex("tuft"), true);
            var mFlow = LitMaterial("M_Flowers", Tex("flowers"), true);
            var mRock = LitMaterial("M_Rock", Tex("rock"), true);
            var mLamp = LitMaterial("M_Lantern", Tex("lantern"), true);
            mLamp.SetTexture("_EmissionMap", Tex("lantern_emission"));
            mLamp.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.40f) * 4f);
            mLamp.EnableKeyword("_EMISSION");
            mLamp.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            bool NearPath(int x, int z)
            {
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx >= 0 && nz >= 0 && nx < Cols && nz < Rows && At(nx, nz) == ',') return true;
                }
                return false;
            }

            int pathCount = 0;
            for (int z = 0; z < Rows; z++)
            for (int x = 0; x < Cols; x++)
            {
                char c = At(x, z);
                Vector3 center = CellCenter(x, z);
                Vector3 J(float s) => center + new Vector3((R() - 0.5f) * s, 0, (R() - 0.5f) * s);

                if (c == '~') continue;
                if (c == ',')
                {
                    if (pathCount++ % 6 == 2)
                    {
                        var p = center + new Vector3(Cell * 0.58f, 0, 0.3f);
                        Billboard("Lantern", quad, mLamp, 12, 44, p, root);
                        var lg = new GameObject("Lantern Light");
                        lg.transform.SetParent(root, false);
                        lg.transform.position = p + new Vector3(0, 2.3f, -0.3f);
                        var l = lg.AddComponent<Light>();
                        l.type = LightType.Point;
                        l.color = new Color(1f, 0.68f, 0.38f);
                        l.range = 6f;
                        l.intensity = 3.5f;
                        l.shadows = LightShadows.None;
                        lg.AddComponent<HD2DFlicker>();
                    }
                    continue;
                }

                int h = HeightAt(x, z);
                bool nearPath = NearPath(x, z);
                if (!nearPath && R() < (h >= 2 ? 0.30f : 0.06f))
                {
                    var t = Billboard("Tree", quad, mTree, 72, 96, J(0.6f), root, 2f);
                    t.AddComponent<HD2DSway>().angle = 0.8f;
                    continue;
                }
                if (R() < 0.10f) { Billboard("Bush", quad, mBush, 20, 14, J(1.2f), root).AddComponent<HD2DSway>(); }
                if (R() < 0.05f) Billboard("Rock", quad, mRock, 16, 12, J(1.2f), root);
                int tufts = (int)(R() * 3.2f);
                for (int i = 0; i < tufts; i++) Billboard("Tuft", quad, mTuft, 12, 10, J(1.7f), root).AddComponent<HD2DSway>().angle = 4f;
                if (R() < 0.18f) Billboard("Flowers", quad, mFlow, 12, 10, J(1.6f), root).AddComponent<HD2DSway>().angle = 3f;
            }
        }

        // ---------------------------------------------------------------- lighting & post
        static void BuildLighting()
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.rotation = Quaternion.Euler(26f, -48f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.80f, 0.58f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            RenderSettings.sun = sun;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.44f, 0.50f, 0.74f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.42f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.20f, 0.26f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.86f, 0.78f, 0.76f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 75f;
        }

        static Volume BuildVolume()
        {
            string path = $"{GenDir}/HD2D_PostProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            T Add<T>() where T : VolumeComponent
            {
                var comp = profile.Add<T>(true);
                comp.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(comp, profile);
                return comp;
            }

            var bloom = Add<Bloom>();
            bloom.threshold.value = 0.85f;
            bloom.intensity.value = 1.3f;
            bloom.scatter.value = 0.65f;
            bloom.tint.value = new Color(1f, 0.93f, 0.82f);

            var dof = Add<DepthOfField>();
            dof.mode.value = DepthOfFieldMode.Bokeh;
            dof.focusDistance.value = 20f;
            dof.focalLength.value = 160f;
            dof.aperture.value = 2.4f;

            var vig = Add<Vignette>();
            vig.intensity.value = 0.32f;
            vig.smoothness.value = 0.45f;
            vig.color.value = new Color(0.08f, 0.06f, 0.14f);

            var ca = Add<ColorAdjustments>();
            ca.postExposure.value = 0.15f;
            ca.contrast.value = 12f;
            ca.saturation.value = 8f;
            ca.colorFilter.value = new Color(1f, 0.98f, 0.94f);

            var tone = Add<Tonemapping>();
            tone.mode.value = TonemappingMode.Neutral;

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("Global Volume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
            return vol;
        }

        /// <summary>
        /// The project's URP asset uses the 2D Renderer, which ignores 3D lights and shadows.
        /// Add a 3D (Universal/Forward) renderer to the same URP asset's renderer list — the default
        /// stays the 2D renderer, only the demo camera opts into the new one.
        /// </summary>
        static int EnsureForwardRenderer()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) { Debug.LogWarning("[HD2D] No URP asset active"); return -1; }

            string path = $"{GenDir}/HD2D_ForwardRenderer.asset";
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (rd == null)
            {
                rd = ScriptableObject.CreateInstance<UniversalRendererData>();
                rd.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(rd, path);
            }

            var so = new SerializedObject(urp);
            var list = so.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == rd) return i;

            int idx = list.arraySize;
            list.InsertArrayElementAtIndex(idx);
            list.GetArrayElementAtIndex(idx).objectReferenceValue = rd;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(urp);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HD2D] Added 3D renderer to {urp.name} at index {idx}");
            return idx;
        }

        static void BuildCamera(Transform target, Volume volume)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.78f, 0.76f);
            go.AddComponent<AudioListener>();

            var data = cam.GetUniversalAdditionalCameraData();
            int rendererIndex = EnsureForwardRenderer();
            if (rendererIndex >= 0) data.SetRenderer(rendererIndex);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var rig = go.AddComponent<HD2DCameraRig>();
            rig.target = target;
            rig.volume = volume;
            go.transform.position = target.position + rig.offset;
            go.transform.LookAt(target.position + Vector3.up * rig.lookHeight);
        }

        static void BuildMotes()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetColor("_BaseColor", new Color(1f, 0.95f, 0.7f) * 3f);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            SaveAsset(mat, "M_Motes.mat");

            var go = new GameObject("Ambient Motes");
            go.transform.position = new Vector3(Cols * Cell * 0.5f, 3f, Rows * Cell * 0.5f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = 0.1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.11f);
            main.maxParticles = 250;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(0.7f, 1f, 0.8f));
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 25f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(Cols * Cell, 4f, Rows * Cell);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.4f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
        }
    }
}
