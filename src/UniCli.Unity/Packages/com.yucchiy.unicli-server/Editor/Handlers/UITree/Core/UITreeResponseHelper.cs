using UnityEngine.UIElements;

namespace UniCli.Server.Editor.Handlers
{
    internal static class UITreeResponseHelper
    {
        public static UITreeTargetInfo BuildTargetInfo(VisualElement root, VisualElement element, string selector)
        {
            return new UITreeTargetInfo
            {
                selector = selector,
                type = element.GetType().FullName,
                name = element.name,
                classes = ElementIntrospection.GetClasses(element, true),
                path = ElementIntrospection.GetHierarchyPath(root, element)
            };
        }
    }
}
