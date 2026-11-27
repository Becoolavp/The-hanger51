using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hanger51.EditorTools
{
    [InitializeOnLoad]
    public static class Hanger51WorldMenuOrganizer
    {
        const string Root = "Hanger 51/World/";
        const string CurrentRoot = Root + "Current/";
        const string ArchiveRoot = Root + "Archive/Steps 096-165/";
        const int ArchiveThroughStep = 165;

        sealed class Registration
        {
            public string path;
            public MethodInfo execute;
            public MethodInfo validate;
            public int priority = 1000;
        }

        static Hanger51WorldMenuOrganizer()
        {
            EditorApplication.delayCall += Organize;
        }

        [MenuItem("Hanger 51/World/Current/Reapply World Menu Organization")]
        public static void Reapply()
        {
            Organize();
        }

        static void Organize()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += Organize;
                return;
            }

            BindingFlags flags =
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            MethodInfo remove = typeof(Menu).GetMethod(
                "RemoveMenuItem",
                flags,
                null,
                new[] { typeof(string) },
                null);

            MethodInfo add = typeof(Menu).GetMethod(
                "AddMenuItem",
                flags,
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

            if (remove == null || add == null)
            {
                Debug.LogWarning(
                    "Hanger 51 could not reorganize the World menu because this Unity version does not expose the expected editor menu APIs. No tools were deleted.");
                return;
            }

            Dictionary<string,Registration> found =
                Discover();

            int moved = 0;
            foreach (Registration registration in found.Values)
            {
                if (registration.execute == null)
                    continue;

                string leaf =
                    registration.path.Substring(CurrentRoot.Length);
                string archived =
                    ArchiveRoot + leaf;
                Registration captured = registration;

                Action execute =
                    () => Invoke(captured.execute,false);
                Func<bool> validate =
                    captured.validate != null
                        ? () => Invoke(captured.validate,true)
                        : null;

                try
                {
                    remove.Invoke(null,new object[] { archived });
                    remove.Invoke(null,new object[] { registration.path });
                    add.Invoke(
                        null,
                        new object[]
                        {
                            archived,
                            string.Empty,
                            false,
                            registration.priority,
                            execute,
                            validate
                        });
                    moved++;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "Hanger 51 could not archive World menu item '" +
                        registration.path +
                        "'. The original tool remains available. " +
                        Meaningful(exception));
                }
            }

            if (moved > 0)
            {
                Debug.Log(
                    "Hanger 51 World menu reorganized. " +
                    moved +
                    " completed World steps through 165 are now under World/Archive/Steps 096-165. New work starts in World/Current at Step 166. Nothing was deleted.");
            }
        }

        static Dictionary<string,Registration> Discover()
        {
            Dictionary<string,Registration> result =
                new Dictionary<string,Registration>(
                    StringComparer.Ordinal);

            Assembly[] assemblies =
                AppDomain.CurrentDomain.GetAssemblies();

            for (int a=0;a<assemblies.Length;a++)
            {
                Type[] types =
                    LoadableTypes(assemblies[a]);

                for (int t=0;t<types.Length;t++)
                {
                    Type type = types[t];
                    if (type == null)
                        continue;

                    MethodInfo[] methods;
                    try
                    {
                        methods = type.GetMethods(
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);
                    }
                    catch
                    {
                        continue;
                    }

                    for (int m=0;m<methods.Length;m++)
                    {
                        MethodInfo method =
                            methods[m];
                        IList<CustomAttributeData> attributes;
                        try
                        {
                            attributes =
                                method.GetCustomAttributesData();
                        }
                        catch
                        {
                            continue;
                        }

                        for (int i=0;i<attributes.Count;i++)
                        {
                            CustomAttributeData attribute =
                                attributes[i];

                            if (attribute.AttributeType != typeof(MenuItem) ||
                                attribute.ConstructorArguments.Count < 1)
                                continue;

                            string path =
                                attribute.ConstructorArguments[0].Value
                                as string;

                            if (string.IsNullOrEmpty(path) ||
                                !path.StartsWith(CurrentRoot,StringComparison.Ordinal))
                                continue;

                            string leaf =
                                path.Substring(CurrentRoot.Length);

                            if (!TryReadStep(leaf,out int step) ||
                                step > ArchiveThroughStep)
                                continue;

                            bool isValidate =
                                attribute.ConstructorArguments.Count >= 2 &&
                                attribute.ConstructorArguments[1].ArgumentType == typeof(bool) &&
                                (bool)attribute.ConstructorArguments[1].Value;

                            int priority =
                                attribute.ConstructorArguments.Count >= 3 &&
                                attribute.ConstructorArguments[2].ArgumentType == typeof(int)
                                    ? (int)attribute.ConstructorArguments[2].Value
                                    : 1000;

                            if (!result.TryGetValue(path,out Registration registration))
                            {
                                registration =
                                    new Registration
                                    {
                                        path = path,
                                        priority = priority
                                    };
                                result.Add(path,registration);
                            }

                            if (isValidate)
                                registration.validate = method;
                            else
                            {
                                registration.execute = method;
                                registration.priority = priority;
                            }
                        }
                    }
                }
            }

            return result;
        }

        static bool TryReadStep(
            string leaf,
            out int step)
        {
            step = -1;
            if (string.IsNullOrWhiteSpace(leaf))
                return false;

            int dash =
                leaf.IndexOf('-');
            string number =
                (dash >= 0 ? leaf.Substring(0,dash) : leaf)
                    .Trim();

            return
                int.TryParse(number,out step) &&
                step >= 0;
        }

        static bool Invoke(
            MethodInfo method,
            bool expectBoolean)
        {
            try
            {
                ParameterInfo[] parameters =
                    method.GetParameters();

                object result;
                if (parameters.Length == 0)
                    result = method.Invoke(null,null);
                else if (parameters.Length == 1 &&
                         parameters[0].ParameterType == typeof(MenuCommand))
                    result = method.Invoke(
                        null,
                        new object[] { new MenuCommand(null) });
                else
                    return false;

                return
                    !expectBoolean ||
                    (result is bool enabled && enabled);
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(
                    exception.InnerException ?? exception);
                return false;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        static Type[] LoadableTypes(
            Assembly assembly)
        {
            if (assembly == null)
                return Array.Empty<Type>();

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                List<Type> good =
                    new List<Type>();

                if (exception.Types != null)
                    for (int i=0;i<exception.Types.Length;i++)
                        if (exception.Types[i] != null)
                            good.Add(exception.Types[i]);

                return good.ToArray();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        static string Meaningful(
            Exception exception)
        {
            if (exception is TargetInvocationException target &&
                target.InnerException != null)
                return target.InnerException.Message;

            return exception.Message;
        }
    }
}
