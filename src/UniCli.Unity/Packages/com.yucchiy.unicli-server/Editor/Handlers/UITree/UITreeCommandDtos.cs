using System;

namespace UniCli.Server.Editor.Handlers
{
    [Serializable]
    public class UITreeDumpRequest
    {
        public string panel;
        public int depth = 10;
        public string filter;
        public bool includeUnityClasses;
    }

    [Serializable]
    public class UITreeDumpResponse
    {
        public UITreePanelInfo panelInfo;
        public string[] lines;
        public int matchedCount;
    }

    [Serializable]
    public class UITreeInspectRequest
    {
        public string panel;
        public string selector;
        public bool includeResolvedStyle = true;
        public bool includeBindingInfo = true;
    }

    [Serializable]
    public class UITreeInspectResponse
    {
        public UITreeElementInfo element;
        public UITreeLayoutInfo layout;
        public UITreeContentInfo content;
        public UITreeStyleEntry[] resolvedStyle;
        public UITreeBindingInfo binding;
    }

    [Serializable]
    public class UITreeClickRequest
    {
        public string panel;
        public string selector;
    }

    [Serializable]
    public class UITreeClickResponse
    {
        public UITreeTargetInfo target;
        public string result;
        public UITreeSideEffectEntry[] sideEffects;
    }

    [Serializable]
    public class UITreeFillRequest
    {
        public string panel;
        public string selector;
        public string value;
    }

    [Serializable]
    public class UITreeFillResponse
    {
        public UITreeTargetInfo target;
        public string oldValue;
        public string newValue;
        public UITreeSideEffectEntry[] sideEffects;
    }

    [Serializable]
    public class UITreeSelectRequest
    {
        public string panel;
        public string selector;
        public string choice;
    }

    [Serializable]
    public class UITreeSelectResponse
    {
        public UITreeTargetInfo target;
        public string oldValue;
        public string newValue;
        public string[] choices;
        public UITreeSideEffectEntry[] sideEffects;
    }
}
