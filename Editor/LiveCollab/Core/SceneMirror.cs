using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace LiveCollab.Core
{
    // ---- serializable scene model (shared by host and clients) ----

    [Serializable]
    public class NodeData
    {
        public int id;
        public string name;
        public int parent;
        public bool active;
        public float px, py, pz;
        public float rx, ry, rz, rw;
        public float sx, sy, sz;
        public List<CompData> comps; // optional component payload (only when changed)

        public static NodeData From(Transform t, int id, int parent)
        {
            var n = new NodeData();
            n.id = id;
            n.name = t.name;
            n.parent = parent;
            n.active = t.gameObject.activeSelf;

            Vector3 p = t.localPosition;
            n.px = p.x; n.py = p.y; n.pz = p.z;
            Quaternion r = t.localRotation;
            n.rx = r.x; n.ry = r.y; n.rz = r.z; n.rw = r.w;
            Vector3 s = t.localScale;
            n.sx = s.x; n.sy = s.y; n.sz = s.z;
            return n;
        }
    }

    [Serializable]
    public class CompData
    {
        public string t; // component type name
        public string j; // EditorJsonUtility json
    }

    [Serializable]
    public class CompDiff
    {
        public int id;   // net node id
        public string t; // component type name
        public string j; // json (empty when removing)
        public bool r;   // true = remove component
    }

    [Serializable]
    public class SceneData
    {
        public List<NodeData> nodes = new List<NodeData>();
    }

    [Serializable]
    public class DiffData
    {
        public int rev;
        public List<NodeData> added = new List<NodeData>();
        public List<NodeData> changed = new List<NodeData>();
        public List<int> removed = new List<int>();
        public List<CompDiff> comps = new List<CompDiff>();
    }

    /// <summary>
    /// Captures the active scene hierarchy, diffs it against the last known
    /// state, and applies remote state back into the scene.
    /// </summary>
    public class SceneMirror
    {
        public const int HighOffsetBase = 1 << 24; // client-created temp ids live here

        public Dictionary<int, NodeData> Nodes = new Dictionary<int, NodeData>();
        public Dictionary<int, GameObject> Objects = new Dictionary<int, GameObject>();
        public int NextId = 1;
        public int Revision;

        public bool AllowMatchExisting;      // client-side: reuse user's objects when possible
        public bool DestroyOnRemove = true;  // destroy objects that disappear
        public bool UseHighOffsetIds;        // client-side: temp ids for locally created objects
        public bool SyncComponents = true;   // sync Inspector/component values
        public int SyncedObjectCount;

        int highSeq;
        readonly Dictionary<int, int> instToNet = new Dictionary<int, int>();
        readonly HashSet<int> consumedInstances = new HashSet<int>();

        // component sync state
        readonly Dictionary<int, Dictionary<string, string>> compStore =
            new Dictionary<int, Dictionary<string, string>>();
        readonly Dictionary<int, HashSet<string>> prevCompPresent =
            new Dictionary<int, HashSet<string>>();
        Dictionary<int, HashSet<string>> currentPresent;

        public void Reset(bool keepScene)
        {
            if (!keepScene)
            {
                foreach (var kv in Objects)
                {
                    if (kv.Value != null)
                    {
                        try { UnityEngine.Object.DestroyImmediate(kv.Value); } catch (Exception) { }
                    }
                }
            }
            Nodes.Clear();
            Objects.Clear();
            instToNet.Clear();
            consumedInstances.Clear();
            compStore.Clear();
            prevCompPresent.Clear();
            currentPresent = null;
            NextId = 1;
            Revision = 0;
            highSeq = 0;
            SyncedObjectCount = 0;
        }

        public List<NodeData> Capture()
        {
            var list = new List<NodeData>();
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return list;
            var present = new Dictionary<int, HashSet<string>>();
            foreach (var root in scene.GetRootGameObjects())
                Walk(root.transform, 0, list, present);
            currentPresent = present;
            return list;
        }

        void Walk(Transform t, int parentNet, List<NodeData> list, Dictionary<int, HashSet<string>> present)
        {
            if (t == null) return;
            int instanceId = t.gameObject.GetInstanceID();
            int id;
            if (!instToNet.TryGetValue(instanceId, out id) || (id >= HighOffsetBase && !UseHighOffsetIds))
            {
                id = UseHighOffsetIds ? (HighOffsetBase + highSeq++) : NextId++;
                instToNet[instanceId] = id;
            }
            var nd = NodeData.From(t, id, parentNet);
            if (SyncComponents) AttachComps(t, nd, id, present);
            list.Add(nd);
            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), id, list, present);
        }

        void AttachComps(Transform t, NodeData nd, int id, Dictionary<int, HashSet<string>> present)
        {
            var seen = new HashSet<string>();
            Dictionary<string, string> store;
            if (!compStore.TryGetValue(id, out store))
            {
                store = new Dictionary<string, string>();
                compStore[id] = store;
            }

            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                string key = c.GetType().FullName;
                if (string.IsNullOrEmpty(key)) continue;
                seen.Add(key);
                try
                {
                    string json = EditorJsonUtility.ToJson(c);
                    string old;
                    if (!store.TryGetValue(key, out old) || old != json)
                    {
                        if (nd.comps == null) nd.comps = new List<CompData>();
                        nd.comps.Add(new CompData { t = key, j = json });
                        store[key] = json;
                    }
                }
                catch (Exception) { }
            }
            present[id] = seen;
        }

        public DiffData Diff(List<NodeData> fresh)
        {
            var diff = new DiffData { rev = Revision + 1 };
            var freshById = new Dictionary<int, NodeData>();
            foreach (var n in fresh)
            {
                if (n.id == 0 || freshById.ContainsKey(n.id)) continue;
                freshById[n.id] = n;
            }

            foreach (var kv in freshById)
            {
                NodeData old;
                if (Nodes.TryGetValue(kv.Key, out old))
                {
                    if (NodeChanged(old, kv.Value)) diff.changed.Add(kv.Value);
                }
                else diff.added.Add(kv.Value);
            }

            foreach (var kv in Nodes)
                if (!freshById.ContainsKey(kv.Key)) diff.removed.Add(kv.Key);

            if (SyncComponents && currentPresent != null)
                ComputeCompDiffs(diff);

            Commit(freshById);
            return diff;
        }

        void ComputeCompDiffs(DiffData diff)
        {
            foreach (var kv in prevCompPresent)
            {
                HashSet<string> cur;
                if (!currentPresent.TryGetValue(kv.Key, out cur)) continue;
                foreach (var typeName in kv.Value)
                {
                    if (!cur.Contains(typeName))
                        diff.comps.Add(new CompDiff { id = kv.Key, t = typeName, r = true });
                }
            }
            prevCompPresent = currentPresent;
            currentPresent = null;
        }

        void Commit(Dictionary<int, NodeData> freshById)
        {
            Nodes.Clear();
            var stale = new List<int>();
            foreach (var kv in Objects)
                if (kv.Value == null) stale.Add(kv.Key);
            foreach (var id in stale) Objects.Remove(id);
            foreach (var kv in freshById) Nodes[kv.Key] = kv.Value;

            // prune component state for objects that are gone
            var dead = new List<int>();
            foreach (var id in compStore.Keys)
                if (!freshById.ContainsKey(id)) dead.Add(id);
            foreach (var id in dead)
            {
                compStore.Remove(id);
                prevCompPresent.Remove(id);
            }

            Revision++;
            SyncedObjectCount = Nodes.Count;
        }

        static bool NodeChanged(NodeData a, NodeData b)
        {
            return a.name != b.name || a.parent != b.parent || a.active != b.active ||
                   !NearlyEqual(a.px, b.px) || !NearlyEqual(a.py, b.py) || !NearlyEqual(a.pz, b.pz) ||
                   !NearlyEqual(a.rx, b.rx) || !NearlyEqual(a.ry, b.ry) ||
                   !NearlyEqual(a.rz, b.rz) || !NearlyEqual(a.rw, b.rw) ||
                   !NearlyEqual(a.sx, b.sx) || !NearlyEqual(a.sy, b.sy) || !NearlyEqual(a.sz, b.sz);
        }

        static bool NearlyEqual(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.00001f;
        }

        // ---------- applying remote state ----------

        public int ApplyScene(SceneData scene)
        {
            if (scene == null || scene.nodes == null) return 0;
            int created = 0;
            var batch = new Dictionary<int, GameObject>();
            foreach (var nd in scene.nodes)
                if (EnsureNode(nd, batch)) created++;
            return created;
        }

        public int ApplyDiff(DiffData diff)
        {
            if (diff == null) return 0;
            int created = 0;
            var batch = new Dictionary<int, GameObject>();
            if (diff.added != null)
            {
                foreach (var nd in diff.added)
                    if (EnsureNode(nd, batch)) created++;
            }

            if (diff.changed != null)
            {
                foreach (var nd in diff.changed)
                {
                    GameObject go;
                    if (batch.TryGetValue(nd.id, out go) && go != null)
                        ApplyToTransform(nd, go.transform);
                    else if (Objects.TryGetValue(nd.id, out go) && go != null)
                        ApplyToTransform(nd, go.transform);
                    else if (EnsureNode(nd, batch)) created++;
                }
            }

            if (diff.removed != null)
            {
                foreach (var id in diff.removed)
                {
                    GameObject go;
                    if (Objects.TryGetValue(id, out go) && go != null)
                    {
                        if (DestroyOnRemove)
                        {
                            try { UnityEngine.Object.DestroyImmediate(go); } catch (Exception) { }
                        }
                        Objects.Remove(id);
                        try { instToNet.Remove(go.GetInstanceID()); } catch (Exception) { }
                    }
                    Nodes.Remove(id);
                }
            }

            if (diff.comps != null)
            {
                foreach (var cd in diff.comps)
                {
                    GameObject go;
                    if (!Objects.TryGetValue(cd.id, out go) || go == null) continue;
                    if (cd.r) RemoveComponentOfType(go, cd.t);
                    else ApplyComponentValue(go, cd.t, cd.j);
                }
            }
            return created;
        }

        bool EnsureNode(NodeData nd, Dictionary<int, GameObject> batch)
        {
            GameObject go;
            if (batch.TryGetValue(nd.id, out go)) return false;
            if (Objects.TryGetValue(nd.id, out go) && go != null)
            {
                batch[nd.id] = go;
                return false;
            }

            go = FindForNode(nd, batch);
            bool created = false;
            if (go == null)
            {
                go = new GameObject(nd.name);
                created = true;
            }
            batch[nd.id] = go;
            bool isTemp = nd.id >= HighOffsetBase;
            if (!isTemp || UseHighOffsetIds) Objects[nd.id] = go;
            instToNet[go.GetInstanceID()] = nd.id;

            if (nd.parent != 0)
            {
                GameObject pg;
                if (batch.TryGetValue(nd.parent, out pg) && pg != null)
                    go.transform.SetParent(pg.transform, true);
                else if (Objects.TryGetValue(nd.parent, out pg) && pg != null)
                    go.transform.SetParent(pg.transform, true);
            }
            ApplyToTransform(nd, go.transform);
            if (nd.comps != null && nd.comps.Count > 0)
            {
                foreach (var cd in nd.comps)
                    ApplyComponentValue(go, cd.t, cd.j);
            }
            return created;
        }

        GameObject FindForNode(NodeData nd, Dictionary<int, GameObject> batch)
        {
            if (!AllowMatchExisting) return null;
            try
            {
                if (nd.parent == 0)
                {
                    foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                    {
                        if (consumedInstances.Contains(root.GetInstanceID())) continue;
                        if (root.name == nd.name)
                        {
                            consumedInstances.Add(root.GetInstanceID());
                            return root;
                        }
                    }
                }
                else
                {
                    GameObject pg;
                    if (batch.TryGetValue(nd.parent, out pg) && pg != null)
                    {
                        for (int i = 0; i < pg.transform.childCount; i++)
                        {
                            var c = pg.transform.GetChild(i);
                            if (consumedInstances.Contains(c.gameObject.GetInstanceID())) continue;
                            if (c.name == nd.name)
                            {
                                consumedInstances.Add(c.gameObject.GetInstanceID());
                                return c.gameObject;
                            }
                        }
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        public static void ApplyToTransform(NodeData nd, Transform t)
        {
            if (t == null) return;
            try
            {
                t.SetLocalPositionAndRotation(
                    new Vector3(nd.px, nd.py, nd.pz),
                    new Quaternion(nd.rx, nd.ry, nd.rz, nd.rw));
                t.localScale = new Vector3(nd.sx, nd.sy, nd.sz);
                if (t.gameObject.activeSelf != nd.active) t.gameObject.SetActive(nd.active);
                if (t.name != nd.name) t.name = nd.name;
            }
            catch (Exception) { }
        }

        // ---------- component sync apply helpers ----------

        static void ApplyComponentValue(GameObject go, string typeName, string json)
        {
            try
            {
                Type type = ResolveType(typeName);
                if (type == null) return;
                var comp = go.GetComponent(type);
                if (comp == null) comp = go.AddComponent(type);
                if (comp == null) return;
                if (!string.IsNullOrEmpty(json))
                    EditorJsonUtility.FromJsonOverwrite(json, comp);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LiveCollab] component apply failed for " + typeName + ": " + e.Message);
            }
        }

        static void RemoveComponentOfType(GameObject go, string typeName)
        {
            try
            {
                Type type = ResolveType(typeName);
                if (type == null) return;
                var comp = go.GetComponent(type);
                if (comp != null) UnityEngine.Object.DestroyImmediate(comp);
            }
            catch (Exception) { }
        }

        static readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

        static Type ResolveType(string name)
        {
            Type t;
            if (typeCache.TryGetValue(name, out t)) return t;
            try { t = Type.GetType(name); } catch (Exception) { t = null; }
            if (t == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        t = asm.GetType(name);
                        if (t != null) break;
                    }
                    catch (Exception) { }
                }
            }
            typeCache[name] = t;
            return t;
        }
    }
}
