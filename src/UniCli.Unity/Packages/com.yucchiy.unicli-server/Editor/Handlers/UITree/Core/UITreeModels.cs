using System;

namespace UniCli.Server.Editor.Handlers
{
    [Serializable]
    public class UITreePanelInfo
    {
        public string name;
        public string title;
        public UITreeSize size;
        public int elementCount;
    }

    [Serializable]
    public class UITreeSize
    {
        public float width;
        public float height;
    }

    [Serializable]
    public class UITreeRect
    {
        public float x;
        public float y;
        public float width;
        public float height;
    }

    [Serializable]
    public class UITreeElementInfo
    {
        public string type;
        public string name;
        public string[] classes;
        public bool enabled;
        public bool visible;
        public bool focusable;
    }

    [Serializable]
    public class UITreeLayoutInfo
    {
        public UITreeRect worldBound;
        public UITreeRect localBound;
    }

    [Serializable]
    public class UITreeContentInfo
    {
        public string text;
        public string value;
    }

    [Serializable]
    public class UITreeStyleEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    public class UITreeBindingInfo
    {
        public string bindingPath;
        public string dataSourceType;
        public bool hasDataSource;
    }

    [Serializable]
    public class UITreeTargetInfo
    {
        public string selector;
        public string type;
        public string name;
        public string[] classes;
        public string path;
    }

    [Serializable]
    public class UITreeSideEffectEntry
    {
        public string path;
        public string summary;
        public string before;
        public string after;
    }

    [Serializable]
    public class UITreeErrorResponse
    {
        public string error;
        public int matchCount;
        public string[] candidates;
    }
}
