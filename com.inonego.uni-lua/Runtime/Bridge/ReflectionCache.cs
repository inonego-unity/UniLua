using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace inonego.UniLua
{

   // ============================================================
   /// <summary>
   /// Describes the kind of a cached member group.
   /// </summary>
   // ============================================================
   internal enum MemberKind
   {
      Property,
      Field,
      Method,
      Event,
      NestedType,
   }

   // ============================================================
   /// <summary>
   /// A cached group of members sharing the same name on a type.
   /// </summary>
   // ============================================================
   internal struct MemberGroup
   {

      public MemberKind Kind;

      public PropertyInfo Property;
      public FieldInfo Field;
      public MethodInfo[] Methods;
      public EventInfo Event;
      public Type NestedType;

   }

   // ============================================================
   /// <summary>
   /// Caches reflection lookups for types, members, constructors,
   /// and provides overload resolution.
   /// Pure C# class with no Lua dependency.
   /// </summary>
   // ============================================================
   internal class ReflectionCache
   {

   #region Fields

      private readonly Dictionary<string, Type> typeCache = new Dictionary<string, Type>();

      private readonly Dictionary<Type, Dictionary<string, MemberGroup>> instanceMemberCache = new Dictionary<Type, Dictionary<string, MemberGroup>>();

      private readonly Dictionary<Type, Dictionary<string, MemberGroup>> staticMemberCache = new Dictionary<Type, Dictionary<string, MemberGroup>>();

      private readonly Dictionary<Type, ConstructorInfo[]> constructorCache = new Dictionary<Type, ConstructorInfo[]>();

      // Overload resolution cache: (methods hashcode + arg type signature) → resolved method
      private readonly Dictionary<long, MethodBase> overloadCache = new Dictionary<long, MethodBase>();

      // Delegate cache: MethodInfo → compiled delegate for fast invocation
      private readonly Dictionary<MethodInfo, Func<object, object[], object>> delegateCache = new Dictionary<MethodInfo, Func<object, object[], object>>();

      private Assembly[] assemblyCache = null;

   #endregion

   #region Type Lookup

      // ------------------------------------------------------------
      /// <summary>
      /// Find a Type by its full name (e.g. "UnityEngine.GameObject").
      /// Scans all loaded assemblies and caches the result.
      /// Returns null if not found.
      /// </summary>
      // ------------------------------------------------------------
      public Type FindType(string fullName)
      {
         if (typeCache.TryGetValue(fullName, out var cached))
         {
            return cached;
         }

         if (assemblyCache == null)
         {
            assemblyCache = AppDomain.CurrentDomain.GetAssemblies();
         }

         for (int i = 0; i < assemblyCache.Length; i++)
         {
            try
            {
               var type = assemblyCache[i].GetType(fullName);

               if (type != null)
               {
                  typeCache[fullName] = type;
                  return type;
               }
            }
            catch
            {
               // Some assemblies may throw on GetType
            }
         }

         // Cache null result to avoid repeated scans
         typeCache[fullName] = null;
         return null;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Refresh the assembly cache. Call when new assemblies may
      /// have been loaded at runtime.
      /// </summary>
      // ------------------------------------------------------------
      public void RefreshAssemblies()
      {
         assemblyCache = AppDomain.CurrentDomain.GetAssemblies();
      }

   #endregion

   #region Member Lookup

      // ------------------------------------------------------------
      /// <summary>
      /// Get cached instance members for a type by member name.
      /// Returns false if no member with that name exists.
      /// </summary>
      // ------------------------------------------------------------
      public bool TryGetInstanceMember(Type type, string name, out MemberGroup group)
      {
         var members = GetOrBuildMemberCache(type, instance: true);
         return members.TryGetValue(name, out group);
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Get cached static members for a type by member name.
      /// Returns false if no member with that name exists.
      /// </summary>
      // ------------------------------------------------------------
      public bool TryGetStaticMember(Type type, string name, out MemberGroup group)
      {
         var members = GetOrBuildMemberCache(type, instance: false);
         return members.TryGetValue(name, out group);
      }

      private Dictionary<string, MemberGroup> GetOrBuildMemberCache(Type type, bool instance)
      {
         var cache = instance ? instanceMemberCache : staticMemberCache;

         if (cache.TryGetValue(type, out var existing))
         {
            return existing;
         }

         var flags = BindingFlags.Public |
            (instance ? BindingFlags.Instance : BindingFlags.Static);

         var dict = new Dictionary<string, MemberGroup>();
         var methodGroups = new Dictionary<string, List<MethodInfo>>();

         // Collect properties
         foreach (var prop in type.GetProperties(flags))
         {
            if (prop.GetIndexParameters().Length > 0)
            {
               continue;
            }

            if (!dict.ContainsKey(prop.Name))
            {
               dict[prop.Name] = new MemberGroup
               {
                  Kind = MemberKind.Property,
                  Property = prop,
               };
            }
         }

         // Collect fields
         foreach (var field in type.GetFields(flags))
         {
            if (!dict.ContainsKey(field.Name))
            {
               dict[field.Name] = new MemberGroup
               {
                  Kind = MemberKind.Field,
                  Field = field,
               };
            }
         }

         // Collect methods (group overloads)
         foreach (var method in type.GetMethods(flags))
         {
            // Skip property accessors
            if (method.IsSpecialName)
            {
               continue;
            }

            if (!methodGroups.TryGetValue(method.Name, out var list))
            {
               list = new List<MethodInfo>();
               methodGroups[method.Name] = list;
            }

            list.Add(method);
         }

         foreach (var kvp in methodGroups)
         {
            if (!dict.ContainsKey(kvp.Key))
            {
               dict[kvp.Key] = new MemberGroup
               {
                  Kind = MemberKind.Method,
                  Methods = kvp.Value.ToArray(),
               };
            }
         }

         // Collect events
         foreach (var evt in type.GetEvents(flags))
         {
            if (!dict.ContainsKey(evt.Name))
            {
               dict[evt.Name] = new MemberGroup
               {
                  Kind = MemberKind.Event,
                  Event = evt,
               };
            }
         }

         // Collect nested types (static only)
         if (!instance)
         {
            foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
            {
               if (!dict.ContainsKey(nested.Name))
               {
                  dict[nested.Name] = new MemberGroup
                  {
                     Kind = MemberKind.NestedType,
                     NestedType = nested,
                  };
               }
            }
         }

         cache[type] = dict;
         return dict;
      }

   #endregion

   #region Constructor

      // ------------------------------------------------------------
      /// <summary>
      /// Get all public constructors for a type.
      /// </summary>
      // ------------------------------------------------------------
      public ConstructorInfo[] GetConstructors(Type type)
      {
         if (constructorCache.TryGetValue(type, out var cached))
         {
            return cached;
         }

         var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
         constructorCache[type] = ctors;
         return ctors;
      }

      // ------------------------------------------------------------
      /// <summary>
      /// Resolve the best constructor match for the given argument
      /// types. Returns null if no match found.
      /// </summary>
      // ------------------------------------------------------------
      public ConstructorInfo ResolveConstructor(Type type, Type[] argTypes)
      {
         var ctors = GetConstructors(type);

         long key = ComputeOverloadKey(ctors, argTypes);

         if (overloadCache.TryGetValue(key, out var cached))
         {
            return (ConstructorInfo)cached;
         }

         var result = (ConstructorInfo)ResolveBestMatch(ctors, argTypes);
         overloadCache[key] = result;
         return result;
      }

   #endregion

   #region Overload Resolution

      // ------------------------------------------------------------
      /// <summary>
      /// Resolve the best method overload for the given argument
      /// types. Returns null if no match found.
      /// </summary>
      // ------------------------------------------------------------
      public MethodInfo ResolveMethod(MethodInfo[] candidates, Type[] argTypes)
      {
         long key = ComputeOverloadKey(candidates, argTypes);

         if (overloadCache.TryGetValue(key, out var cached))
         {
            return (MethodInfo)cached;
         }

         var result = (MethodInfo)ResolveBestMatch(candidates, argTypes);
         overloadCache[key] = result;
         return result;
      }

      private MethodBase ResolveBestMatch(MethodBase[] candidates, Type[] argTypes)
      {
         MethodBase best = null;
         int bestScore = int.MinValue;

         for (int i = 0; i < candidates.Length; i++)
         {
            int score = ScoreMatch(candidates[i], argTypes);

            if (score > bestScore)
            {
               bestScore = score;
               best = candidates[i];
            }
         }

         return bestScore > int.MinValue ? best : null;
      }

      private int ScoreMatch(MethodBase method, Type[] argTypes)
      {
         var parameters = method.GetParameters();

         int requiredCount = 0;

         for (int i = 0; i < parameters.Length; i++)
         {
            if (!parameters[i].IsOptional)
            {
               requiredCount++;
            }
         }

         // Argument count check
         if (argTypes.Length < requiredCount || argTypes.Length > parameters.Length)
         {
            return int.MinValue;
         }

         int score = 0;

         for (int i = 0; i < argTypes.Length; i++)
         {
            var paramType = parameters[i].ParameterType;

            // Handle out/ref
            if (paramType.IsByRef)
            {
               paramType = paramType.GetElementType();
            }

            var argType = argTypes[i];

            // Null argument: compatible with reference types and nullable
            if (argType == null)
            {
               if (paramType.IsValueType && Nullable.GetUnderlyingType(paramType) == null)
               {
                  return int.MinValue;
               }

               score += 2;
               continue;
            }

            // Exact match
            if (paramType == argType)
            {
               score += 3;
               continue;
            }

            // Assignable (inheritance, interface)
            if (paramType.IsAssignableFrom(argType))
            {
               score += 2;
               continue;
            }

            // Numeric widening
            if (IsNumericConversion(argType, paramType))
            {
               score += 1;
               continue;
            }

            // Enum from integer
            if (paramType.IsEnum && IsIntegerType(argType))
            {
               score += 1;
               continue;
            }

            // No match
            return int.MinValue;
         }

         // Prefer methods with fewer optional parameters left unfilled
         score -= (parameters.Length - argTypes.Length);

         return score;
      }

      private static bool IsNumericConversion(Type from, Type to)
      {
         if (from == typeof(int))
         {
            return to == typeof(long) || to == typeof(float) || to == typeof(double);
         }

         if (from == typeof(long))
         {
            return to == typeof(float) || to == typeof(double);
         }

         if (from == typeof(float))
         {
            return to == typeof(double);
         }

         return false;
      }

      private static bool IsIntegerType(Type type)
      {
         return type == typeof(int) || type == typeof(long) ||
                type == typeof(short) || type == typeof(byte);
      }

      private static long ComputeOverloadKey(MethodBase[] candidates, Type[] argTypes)
      {
         unchecked
         {
            long hash = candidates.Length * 397L;

            if (candidates.Length > 0)
            {
               hash ^= candidates[0].GetHashCode() * 17L;
            }

            for (int i = 0; i < argTypes.Length; i++)
            {
               hash = hash * 31L + (argTypes[i]?.GetHashCode() ?? 0);
            }

            hash = hash * 31L + argTypes.Length;
            return hash;
         }
      }

   #endregion

   #region Delegate Cache

      // ------------------------------------------------------------
      /// <summary>
      /// Get or create a fast invocation delegate for a MethodInfo.
      /// Falls back to MethodInfo.Invoke if delegate creation fails.
      /// </summary>
      // ------------------------------------------------------------
      public object FastInvoke(MethodInfo method, object target, object[] args)
      {
         if (delegateCache.TryGetValue(method, out var cached))
         {
            return cached(target, args);
         }

         var invoker = BuildInvoker(method);
         delegateCache[method] = invoker;
         return invoker(target, args);
      }

      private static Func<object, object[], object> BuildInvoker(MethodInfo method)
      {
         // Use Delegate.CreateDelegate for simple cases (no out/ref, no params)
         var parameters = method.GetParameters();
         bool hasRefParams = false;

         for (int i = 0; i < parameters.Length; i++)
         {
            if (parameters[i].ParameterType.IsByRef || parameters[i].IsOut)
            {
               hasRefParams = true;
               break;
            }
         }

         // For methods with out/ref params, fall back to MethodInfo.Invoke
         // (Delegate.CreateDelegate can't handle these generically)
         if (hasRefParams)
         {
            return (obj, a) => method.Invoke(obj, a);
         }

         // Build a typed delegate wrapper
         return BuildTypedInvoker(method, parameters);
      }

      private static Func<object, object[], object> BuildTypedInvoker(MethodInfo method, ParameterInfo[] parameters)
      {
         bool isVoid = method.ReturnType == typeof(void);
         bool isStatic = method.IsStatic;
         int paramCount = parameters.Length;

         // For 0-2 parameters, create specialized delegates for best performance
         try
         {
            if (isStatic && isVoid && paramCount == 0)
            {
               var d = (Action)Delegate.CreateDelegate(typeof(Action), method);
               return (obj, a) => { d(); return null; };
            }

            if (isStatic && isVoid && paramCount == 1)
            {
               var pType = parameters[0].ParameterType;

               if (pType == typeof(object))
               {
                  var d = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), method);
                  return (obj, a) => { d(a[0]); return null; };
               }

               if (pType == typeof(string))
               {
                  var d = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), method);
                  return (obj, a) => { d((string)a[0]); return null; };
               }

               if (pType == typeof(bool))
               {
                  var d = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), method);
                  return (obj, a) => { d((bool)a[0]); return null; };
               }
            }

            if (!isStatic && isVoid && paramCount == 1)
            {
               var pType = parameters[0].ParameterType;

               if (pType == typeof(bool))
               {
                  // Common pattern: obj.SetActive(bool)
                  return (obj, a) =>
                  {
                     method.Invoke(obj, a);
                     return null;
                  };
               }
            }
         }
         catch
         {
            // Delegate creation failed, fall through to Invoke
         }

         // General fallback: still faster than raw Invoke due to caching the MethodInfo lookup
         return (obj, a) => method.Invoke(obj, a);
      }

   #endregion

   }
}
