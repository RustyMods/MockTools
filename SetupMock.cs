using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.Experimental.SceneManagement;
using UnityEditor.SceneManagement;
using Component = UnityEngine.Component;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
[ExecuteInEditMode]
public class SetupMock : MonoBehaviour
{
    public List<GameObject> ZNetSceneObjects = new();
    public List<Shader> Shaders = new();
    public List<Shader> MockShaders = new();

    public List<string> MockShaderPrefixes = new()
    {
        "JVLmock_"
    };

    private readonly Dictionary<string, Shader> m_namedShaders = new();

    private Dictionary<string, Shader> NamedShaders
    {
        get
        {
            if (m_namedShaders.Count > 0) return m_namedShaders;
            foreach (var shader in Shaders)
            {
                m_namedShaders[shader.name] = shader;
            }
            return m_namedShaders;
        }
    }
    private readonly Dictionary<Shader, Shader> m_shaderReplacements = new Dictionary<Shader, Shader>();
    private Dictionary<Shader, Shader> ShaderReplacements
    {
        get
        {
            if (m_shaderReplacements.Count > 0) return m_shaderReplacements;
            foreach (var shader in MockShaders)
            {
                var name = shader.name;
                foreach (var prefix in MockShaderPrefixes)
                {
                    name = name.Replace(prefix, "");
                }
                if (!NamedShaders.TryGetValue(name, out Shader? original)) continue;
                m_shaderReplacements[original] = shader;
            }
            return m_shaderReplacements;
        }
    }

    [HideInInspector]
    private HashSet<string>? m_sceneObjectNames;

    [HideInInspector]
    private HashSet<string> SceneObjectNames
    {
        get
        {
            m_sceneObjectNames ??= new HashSet<string>(ZNetSceneObjects.ConvertAll(obj => obj.name));
            return m_sceneObjectNames;
        }
    }
    public string prefix = "MOCK_";
    public string postfix = "";

    [ContextMenu("Get Shaders")]
    public void FindShaders()
    {
        string[] guids = AssetDatabase.FindAssets("t:Shader");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader != null)
            {
                if (string.IsNullOrWhiteSpace(shader.name)) continue;
                if (shader.name.StartsWith("JVLmock_"))
                {
                    MockShaders.Add(shader);
                }
                else if (!shader.name.StartsWith("Custom")) continue;
                Shaders.Add(shader);
            }
        }
        MarkDirtyIfChanged();
    }

    [ContextMenu("Replace Shaders")]
    public void ReplaceShaders()
    {
        PrefabStage ps = PrefabStageUtility.GetPrefabStage(transform.root.gameObject);
        string? directory = Path.GetDirectoryName(ps.assetPath);
        if (!string.IsNullOrEmpty(directory)) return;
        var materialFolder = Path.Combine(directory, "Generated_Mock_Materials");
        if (!System.IO.Directory.Exists(materialFolder))
        {
            System.IO.Directory.CreateDirectory(materialFolder);
            AssetDatabase.Refresh();
        }
        
        var renderers = transform.gameObject.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            List<Material> materials = new();
            foreach (var material in renderer.sharedMaterials)
            {
                var materialPath = Path.Combine(materialFolder, prefix + material.name + postfix + ".mat");
                Material mat = (Material)AssetDatabase.LoadAssetAtPath(materialPath, typeof(Material));
                if (mat != null)
                {
                    materials.Add(mat);
                    continue;
                }

                if (!ShaderReplacements.TryGetValue(material.shader, out var shaderReplacement))
                {
                    materials.Add(material);
                    continue;
                }
                mat = new Material(shaderReplacement);
                
                CopyProperties(material, mat);
                
                AssetDatabase.CreateAsset(mat, materialPath);
                materials.Add(mat);
            }

            renderer.sharedMaterials = materials.ToArray();
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    public static void CopyProperties(Material source, Material target)
    {
        if (source == null || target == null)
        {
            Debug.LogError("Source or target material is null.");
            return;
        }

        Shader sourceShader = source.shader;
        int propertyCount = ShaderUtil.GetPropertyCount(sourceShader);

        for (int i = 0; i < propertyCount; i++)
        {
            string propName = ShaderUtil.GetPropertyName(sourceShader, i);
            ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(sourceShader, i);

            if (!target.HasProperty(propName)) continue;

            switch (propType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    target.SetColor(propName, source.GetColor(propName));
                    break;
                case ShaderUtil.ShaderPropertyType.Vector:
                    target.SetVector(propName, source.GetVector(propName));
                    break;
                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    target.SetFloat(propName, source.GetFloat(propName));
                    break;
                case ShaderUtil.ShaderPropertyType.TexEnv:
                    target.SetTexture(propName, source.GetTexture(propName));
                    target.SetTextureOffset(propName, source.GetTextureOffset(propName));
                    target.SetTextureScale(propName, source.GetTextureScale(propName));
                    break;
            }
        }
    }
    
    [ContextMenu("Get ZNetScene Objects")]
    public void FindObjects()
    {
        string[] guids = AssetDatabase.FindAssets("_GameMain t:Prefab");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var scenePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var scene = scenePrefab.transform.Find("_NetScene");
            if (scene == null) return;
            var component = scene.GetComponent<ZNetScene>();
            if (component == null) return;
            ZNetSceneObjects = component.m_prefabs;
        }

        MarkDirtyIfChanged();
    }
    
    [ContextMenu("Rename")]
    public void Rename()
    {
        foreach (Transform child in transform)
        {
            if (!IsSceneObject(child.name)) continue;

            string nameWithoutInstanceNumber = child.name;

            int lastParenIndex = nameWithoutInstanceNumber.LastIndexOf('(');
            if (lastParenIndex > 0 && nameWithoutInstanceNumber.EndsWith(")"))
            {
                nameWithoutInstanceNumber = nameWithoutInstanceNumber.Substring(0, lastParenIndex).TrimEnd();
            }

            child.name = prefix + nameWithoutInstanceNumber + postfix;
        }

        MarkDirtyIfChanged();
    }
    public bool IsSceneObject(string name)
    {
        int spaceIndex = name.IndexOf(' ');
        if (spaceIndex >= 0)
            name = name.Substring(0, spaceIndex);

        return SceneObjectNames.Contains(name);
    }
    
    [ContextMenu("Cleanup")]
    public void Cleanup()
    {
        foreach (Transform child in transform)
        {
            if (!IsSceneObject(child.name.Replace(prefix, string.Empty))) continue;
            DestroyAllChildrenImmediate(child);
            RemoveAllComponents(child.gameObject);
        }

        MarkDirtyIfChanged();
    }
    
    public void MarkDirtyIfChanged()
    {
        if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
    
    public void RemoveAllComponents(GameObject go)
    {
        var components = new List<Component>(go.GetComponents<Component>());
        components.RemoveAll(c => c is Transform);
        components.Sort((a, b) => DependencyDepth(b).CompareTo(DependencyDepth(a)));
        foreach (var comp in components)
        {
            if (comp != null) DestroyImmediate(comp, true);
        }
    }
    public int DependencyDepth(Component comp)
    {
        var type = comp.GetType();
        var attrs = (RequireComponent[])type.GetCustomAttributes(typeof(RequireComponent), true);
        if (attrs.Length == 0) return 0;
        return 1 + attrs.Length; // crude depth estimation
    }

    public void DestroyAllChildrenImmediate(Transform transform)
    {
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in transform)
        {
            children.Add(child.gameObject);
        }

        foreach (GameObject child in children)
        {
            DestroyImmediate(child);
        }
    }
}