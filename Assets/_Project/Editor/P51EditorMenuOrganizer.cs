using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hanger51.EditorTools
{
    [InitializeOnLoad]
    public static class P51EditorMenuOrganizer
    {
        private const string Root = "Hanger 51/P-51 Mustang/";
        private const string CurrentRoot = Root + "Current/";
        private const string LegacyRoot = Root + "Legacy Setup/";

        private sealed class MenuRegistration
        {
            internal string OriginalPath;
            internal MethodInfo ExecuteMethod;
            internal MethodInfo ValidateMethod;
            internal int Priority = 1000;
        }

        static P51EditorMenuOrganizer()
        {
            EditorApplication.delayCall += OrganizeMenus;
        }

        [MenuItem("Hanger 51/P-51 Mustang/Current/Reapply P-51 Menu Organization")]
        public static void ReapplyMenuOrganization()
        {
            OrganizeMenus();
        }

        private static void OrganizeMenus()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += OrganizeMenus;
                return;
            }

            BindingFlags menuFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo removeMenuItem = typeof(Menu).GetMethod(
                "RemoveMenuItem",
                menuFlags,
                null,
                new[] { typeof(string) },
                null);
            MethodInfo addMenuItem = typeof(Menu).GetMethod(
                "AddMenuItem",
                menuFlags,
                null,
                new[]
                {
                    typeof(string),
                    typeof(string),
                    typeof(bool),
                    typeof(int),
                    typeof(Action),
                    typeof(Func<bool>)
                },
                null);

            if (removeMenuItem == null || addMenuItem == null)
            {
                Debug.LogWarning(
                    "Hanger 51 could not organize the P-51 editor menu because this Unity version did not expose the expected internal menu registration methods. "
                    + "No project files or existing menu commands were changed.");
                return;
            }

            Dictionary<string, MenuRegistration> registrations = DiscoverLegacyRegistrations();
            int moved = 0;

            foreach (KeyValuePair<string, MenuRegistration> pair in registrations)
            {
                MenuRegistration registration = pair.Value;
                if (registration == null || registration.ExecuteMethod == null)
                {
                    continue;
                }

                string legacyPath = LegacyRoot + registration.OriginalPath.Substring(Root.Length);
                MenuRegistration captured = registration;
                Action execute = () => InvokeMenuMethod(captured.ExecuteMethod, false);
                Func<bool> validate = captured.ValidateMethod != null
                    ? () => InvokeMenuMethod(captured.ValidateMethod, true)
                    : null;

                try
                {
                    // Domain reloads rebuild attribute menus. Remove both locations first so this
                    // remains idempotent when the user manually reapplies the organization.
                    removeMenuItem.Invoke(null, new object[] { legacyPath });
                    removeMenuItem.Invoke(null, new object[] { registration.OriginalPath });
                    addMenuItem.Invoke(
                        null,
                        new object[]
                        {
                            legacyPath,
                            string.Empty,
                            false,
                            registration.Priority,
                            execute,
                            validate
                        });
                    moved++;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"Hanger 51 could not move editor command '{registration.OriginalPath}' into Legacy Setup. "
                        + $"The command itself was not deleted. {GetMeaningfulMessage(exception)}");
                }
            }

            if (moved > 0)
            {
                Debug.Log(
                    $"Hanger 51 P-51 menu organized: {moved} older setup/repair command(s) are now under 'P-51 Mustang/Legacy Setup'. "
                    + "Current P-51 tools remain under 'P-51 Mustang/Current'. No runtime code, scenes, assets or recovery methods were removed.");
            }
        }

        private static Dictionary<string, MenuRegistration> DiscoverLegacyRegistrations()
        {
            Dictionary<string, MenuRegistration> registrations = new Dictionary<string, MenuRegistration>(StringComparer.Ordinal);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types = GetLoadableTypes(assemblies[assemblyIndex]);
                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type == null)
                    {
                        continue;
                    }

                    MethodInfo[] methods;
                    try
                    {
                        methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                    catch
                    {
                        continue;
                    }

                    for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                    {
                        MethodInfo method = methods[methodIndex];
                        IList<CustomAttributeData> attributes;
                        try
                        {
                            attributes = method.GetCustomAttributesData();
                        }
                        catch
                        {
                            continue;
                        }

                        for (int attributeIndex = 0; attributeIndex < attributes.Count; attributeIndex++)
                        {
                            CustomAttributeData attribute = attributes[attributeIndex];
                            if (attribute.AttributeType != typeof(MenuItem) || attribute.ConstructorArguments.Count < 1)
                            {
                                continue;
                            }

                            string path = attribute.ConstructorArguments[0].Value as string;
                            if (string.IsNullOrEmpty(path)
                                || !path.StartsWith(Root, StringComparison.Ordinal)
                                || path.StartsWith(CurrentRoot, StringComparison.Ordinal)
                                || path.StartsWith(LegacyRoot, StringComparison.Ordinal))
                            {
                                continue;
                            }

                            bool isValidate = attribute.ConstructorArguments.Count >= 2
                                && attribute.ConstructorArguments[1].ArgumentType == typeof(bool)
                                && (bool)attribute.ConstructorArguments[1].Value;
                            int priority = attribute.ConstructorArguments.Count >= 3
                                && attribute.ConstructorArguments[2].ArgumentType == typeof(int)
                                ? (int)attribute.ConstructorArguments[2].Value
                                : 1000;

                            if (!registrations.TryGetValue(path, out MenuRegistration registration))
                            {
                                registration = new MenuRegistration
                                {
                                    OriginalPath = path,
                                    Priority = priority
                                };
                                registrations.Add(path, registration);
                            }

                            if (isValidate)
                            {
                                registration.ValidateMethod = method;
                            }
                            else
                            {
                                registration.ExecuteMethod = method;
                                registration.Priority = priority;
                            }
                        }
                    }
                }
            }

            return registrations;
        }

        private static bool InvokeMenuMethod(MethodInfo method, bool expectBoolean)
        {
            if (method == null)
            {
                return !expectBoolean;
            }

            try
            {
                ParameterInfo[] parameters = method.GetParameters();
                object result;
                if (parameters.Length == 0)
                {
                    result = method.Invoke(null, null);
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(MenuCommand))
                {
                    result = method.Invoke(null, new object[] { new MenuCommand(null) });
                }
                else
                {
                    Debug.LogError(
                        $"Hanger 51 cannot invoke legacy menu method '{method.DeclaringType?.FullName}.{method.Name}' because it has an unsupported parameter list.");
                    return false;
                }

                if (!expectBoolean)
                {
                    return true;
                }

                return result is bool enabled && enabled;
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception);
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            if (assembly == null)
            {
                return Array.Empty<Type>();
            }

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                Type[] source = exception.Types;
                if (source == null || source.Length == 0)
                {
                    return Array.Empty<Type>();
                }

                List<Type> loadable = new List<Type>(source.Length);
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] != null)
                    {
                        loadable.Add(source[i]);
                    }
                }
                return loadable.ToArray();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static string GetMeaningfulMessage(Exception exception)
        {
            if (exception is TargetInvocationException target && target.InnerException != null)
            {
                return target.InnerException.Message;
            }
            return exception.Message;
        }
    }
}
