﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Meta.WitAi;
using Meta.WitAi.Json;

namespace Meta.Conduit
{
    /// <summary>
    /// The manifest is the core artifact generated by Conduit that contains the relevant information about the app.
    /// This information can be used to train the backend or dispatch incoming requests to methods.
    /// </summary>
    internal class Manifest
    {
        /// <summary>
        /// Called via JSON reflection, need preserver or it will be stripped on compile
        /// </summary>
        [UnityEngine.Scripting.Preserve]
        public Manifest() { }

        /// <summary>
        /// The App ID.
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// The version of the Manifest format.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// A human friendly name for the application/domain.
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// List of relevant entities.
        /// </summary>
        public List<ManifestEntity> Entities { get; set; } = new List<ManifestEntity>();

        /// <summary>
        /// List of relevant actions (methods).
        /// </summary>
        public List<ManifestAction> Actions { get; set; } = new List<ManifestAction>();

        /// <summary>
        /// Maps action IDs (intents) to CLR methods. Each entry in the value list is a different overload of the method.
        /// The list is sorted with the most parameters listed first, so we get maximal matches during dispatching by
        /// default without needing to sort them at runtime.
        /// </summary>
        private readonly Dictionary<string, List<InvocationContext>> _methodLookup =
            new Dictionary<string, List<InvocationContext>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// If entities are resolved, this will hold their data types.
        /// This will be empty if entities were not explicitly resolved.
        /// </summary>
        public Dictionary<string, Type> CustomEntityTypes { get; } = new Dictionary<string, Type>();

        public bool ResolveEntities()
        {
            bool allResolved = true;
            foreach (var entity in Entities)
            {
                var typeName = string.IsNullOrEmpty(entity.Namespace) ? entity.ID : $"{entity.Namespace}.{entity.ID}";
                
                var qualifiedTypeName = $"{typeName},{entity.Assembly}";
                var type = Type.GetType(qualifiedTypeName);
                if (type == null)
                {
                    VLog.E($"Failed to resolve type: {qualifiedTypeName}");
                    allResolved = false;
                }
                CustomEntityTypes[entity.Name] = type;
            }

            return allResolved;
        }
        
        /// <summary>
        /// Processes all actions in the manifest and associate them with the methods they should invoke.
        /// </summary>
        public bool ResolveActions()
        {
            var resolvedAll = true;
            foreach (var action in this.Actions)
            {
                var lastPeriod = action.ID.LastIndexOf('.');
                if (lastPeriod <= 0)
                {
                    VLog.E($"Invalid Action ID: {action.ID}");
                    resolvedAll = false;
                    continue;
                }

                var typeName = action.ID.Substring(0, lastPeriod);
                var qualifiedTypeName = $"{typeName},{action.Assembly}";
                var method = action.ID.Substring(lastPeriod + 1);

                var targetType = Type.GetType(qualifiedTypeName);
                if (targetType == null)
                {
                    VLog.E($"Failed to resolve type: {qualifiedTypeName}");
                    resolvedAll = false;
                    continue;
                }

                var types = new Type[action.Parameters.Count];
                for (var i = 0; i < action.Parameters.Count; i++)
                {
                    var manifestParameter = action.Parameters[i];
                    var fullTypeName = $"{manifestParameter.QualifiedTypeName},{manifestParameter.TypeAssembly}";
                    types[i] = Type.GetType(fullTypeName);
                    if (types[i] == null)
                    {
                        VLog.E($"Failed to resolve type: {fullTypeName}");
                    }
                }

                var targetMethod = GetBestMethodMatch(targetType, method, types);
                if (targetMethod == null)
                {
                    VLog.E($"Failed to resolve method {typeName}.{method}.");
                    resolvedAll = false;
                    continue;
                }

                var attributes = targetMethod.GetCustomAttributes(typeof(ConduitActionAttribute), false);
                if (attributes.Length == 0)
                {
                    VLog.E($"{targetMethod} - Did not have expected Conduit attribute");
                    resolvedAll = false;
                    continue;
                }
                var actionAttribute = attributes.First() as ConduitActionAttribute;

                var invocationContext = new InvocationContext()
                {
                    Type = targetType,
                    MethodInfo = targetMethod,
                    MinConfidence = actionAttribute.MinConfidence,
                    MaxConfidence = actionAttribute.MaxConfidence,
                    ValidatePartial = actionAttribute.ValidatePartial
                };

                if (!_methodLookup.ContainsKey(action.Name))
                {
                    _methodLookup.Add(action.Name, new List<InvocationContext>());
                }

                _methodLookup[action.Name].Add(invocationContext);
            }

            foreach (var invocationContext in _methodLookup.Values.Where(invocationContext =>
                         invocationContext.Count > 1))
            {
                // This is a slow operation. If there multiple overloads are common, we should optimize this
                invocationContext.Sort((one, two) =>
                    two.MethodInfo.GetParameters().Length - one.MethodInfo.GetParameters().Length);
            }

            return resolvedAll;
        }

        private MethodInfo GetBestMethodMatch(Type targetType, string method, Type[] parameterTypes)
        {
            var exactMatch = targetType.GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, null, CallingConventions.Any,
                parameterTypes, null);

            return exactMatch;
        }

        /// <summary>
        /// Returns true if the manifest contains the specified action.
        /// </summary>
        /// <param name="action"></param>
        /// <returns>True if the action exists, false otherwise.</returns>
        public bool ContainsAction(string @action)
        {
            return _methodLookup.ContainsKey(action);
        }

        /// <summary>
        /// Returns the invocation context for the specified action ID.
        /// </summary>
        /// <param name="actionId">The action ID.</param>
        /// <returns>The invocationContext.</returns>
        public List<InvocationContext> GetInvocationContexts(string actionId)
        {
            return _methodLookup[actionId];
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
