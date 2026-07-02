namespace UnityEditor.XR.Management
{
    static class TypeLoaderExtensions
    {
        internal static TypeCache.TypeCollection GetAllTypesWithInterface<T>()
        {
            return TypeCache.GetTypesDerivedFrom(typeof(T));
        }

        internal static TypeCache.TypeCollection GetAllTypesWithAttribute<T>()
        {
            return TypeCache.GetTypesWithAttribute(typeof(T));
        }
    }
}
