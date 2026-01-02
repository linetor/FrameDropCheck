using System.Reflection;

namespace FrameDropCheck.Plugin.Services
{
    /// <summary>
    /// Helper extension to safely get types from an assembly without throwing on reflection load errors.
    /// </summary>
    internal static class ReflectionExtensions
    {
        public static IEnumerable<Type> GetTypesSafe(this Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException rtl)
            {
                return rtl.Types.Where(t => t != null)!;
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }
    }
}
