// Copyright (c) The University of Dundee 2018-2019
// This file is part of the Research Data Management Platform (RDMP).
// RDMP is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// RDMP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
// You should have received a copy of the GNU General Public License along with RDMP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Rdmp.Core.Curation.Data.Pipelines;
using Rdmp.Core.DataFlowPipeline.Requirements.Exceptions;
using ReusableLibraryCode.Progress;

namespace Rdmp.Core.DataFlowPipeline.Requirements
{
    /// <summary>
    /// Low level description of what an IPipeline must look like to be compatible with a given use case for a IDataFlowPipelineEngine.  This includes whether there must be
    /// a specific base type / interface for source / destination components as well as what the flow T object is (e.g. System.Data.DataTable).  
    /// 
    /// <para>This class also handles distributing initialization object instances to subscribers (components implementing IPipelineRequirement X).  You can create one of these
    /// with DataFlowPipelineContextFactory but really you should only be doing this if you are building a new IPipelineUseCase.  If you are trying to run an IPipeline that
    /// is used elsewhere in RDMP then you need to find the IPipelineUseCase that matches the job you are trying to achieve.</para>
    /// 
    /// <para>DataFlowPipelineContext is symantically similar to IPipelineUseCase, the difference is that the context only contains low level rules about what is compatible while
    /// the IPipelineUseCase also has the specific objects that will be used for initialization, fixed source instances etc (as well the context).</para>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class DataFlowPipelineContext<T>: IDataFlowPipelineContext
    {

        /// <summary>
        /// Optional.  Specifies that in order for an <see cref="IPipeline"/> to be compatible with the context, its <see cref="IPipeline.Source"/> must inherit/implement the given Type
        /// </summary>
        public Type MustHaveSource { get; set; }

        /// <summary>
        /// Optional.  Specifies that in order for an <see cref="IPipeline"/> to be compatible with the context, its <see cref="IPipeline.Destination"/> must inherit/implement the given Type
        /// </summary>
        public Type MustHaveDestination { get; set; }

        /// <inheritdoc/>
        public HashSet<Type> CannotHave { get; private set; }
        
        /// <summary>
        /// Creates a new empty context for determining <see cref="IPipeline"/> compatibility
        /// </summary>
        public DataFlowPipelineContext()
        {
            CannotHave = new HashSet<Type>();
        }

        /// <summary>
        /// Returns true the Type <paramref name="t"/> would be allowed by the context e.g. it doesn't implement an <see cref="CannotHave"/> and it is compatible with the flow Type {T} etc
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        public bool IsAllowable(Type t)
        {
            string whoCares;
            return IsAllowable(t, out whoCares);
        }
        
        /// <inheritdoc/>
        public bool IsAllowable(Type t, out string reason)
        {
            //if t is a source
            if (typeof(IDataFlowSource<T>).IsAssignableFrom(t))
                if (MustHaveSource == null) //we do not allow sources!
                {
                    reason = "The current context does not allow for IDataFlowSources";
                    return false;
                }
                else
                {
                    reason = null;
                    var isAssignable = MustHaveSource.IsAssignableFrom(t);
                    if (!isAssignable)
                        reason = "Type " + GetFullName(t) + " is not a " + GetFullName(MustHaveSource) + " (which is the required type for all sources within the current context)";

                    return isAssignable;
                }

            //if t is a destination
            if (typeof(IDataFlowDestination<T>).IsAssignableFrom(t))
                if (MustHaveDestination == null) // we do not allow destinations
                {
                    reason = "The current context does not allow for IDataFlowDestinations";
                    return false;
                }
                else
                {
                    reason = null;
                    var isAssignable = MustHaveDestination.IsAssignableFrom(t);
                    if (!isAssignable)
                        reason = "Type " + GetFullName(t) +  " is not a " + GetFullName(MustHaveDestination) + " (which is the required type for all destinations within the current context)";

                    return isAssignable;
                }


            //if there are any forbidden types further up the inheritance heirarchy than t reject it
            foreach (var forbiddenType in CannotHave)
            {
                if (forbiddenType.IsAssignableFrom(t))
                {
                    reason = "Type " + GetFullName(t) + " is inherited from a type which is forbidden in the current context: " + GetFullName(forbiddenType);
                    return false;
                }

                if (forbiddenType.IsInterface //or forbidden thing is IPipelineRequirement<TableInfo>
                    &&
                    forbiddenType.IsGenericType
                    &&
                    forbiddenType.GenericTypeArguments.Any(subtype => subtype.IsAssignableFrom(t))) //and t is TableInfo (generic <T> == t)
                {
                    reason = "Type " + GetFullName(t) + " is a generic parameter of forbidden type " + GetFullName(forbiddenType) + "(Forbidden within the current context)";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private bool IsAllowable(Type t, out Type forbiddenType)
        {
            forbiddenType = CannotHave.FirstOrDefault(type => type.IsAssignableFrom(t));

            return forbiddenType == null;
        }

        /// <inheritdoc/>
        public bool IsAllowable(IPipeline pipeline, out string reason)
        {
            foreach (var component in pipeline.PipelineComponents)
            {
                Type forbiddenType;

                if (!IsAllowable(component.GetClassAsSystemType(), out forbiddenType))
                {
                    reason = "Component " + component.Name + " implements a forbidden type (" + GetFullName(forbiddenType) + ") under the pipeline usage context";
                    return false;
                }
            }

            reason = MustHave(MustHaveDestination, pipeline.Destination, "destination");
            if (reason != null)
                return false;

            reason = MustHave(MustHaveSource, pipeline.Source, "source");
            return reason == null;


        }

        private string MustHave(Type mustHaveType, IPipelineComponent component, string descriptionOfThingBeingChecked)
        {
            //it must have destination
            if (mustHaveType != null)
                if (component == null)
                    return "An explicit " + descriptionOfThingBeingChecked + " must be chosen";
                else
                {
                    Type pipelineComponentType = component.GetClassAsSystemType();

                    if (pipelineComponentType == null)
                        return "PipelineComponent " + component.Class + " could not be created, check MEF assembly loading in the Diagnostics menu";

                    if (!mustHaveType.IsAssignableFrom(pipelineComponentType))
                    {
                        return "The pipeline requires a " + descriptionOfThingBeingChecked + " of type " + GetFullName(mustHaveType) +
                                 " but the currently configured " + descriptionOfThingBeingChecked + GetFullName(pipelineComponentType) +
                                 " is not of the same type or a derrived type";
                    }
                }
            else
             //it cannot have destination 
                if (component != null)
                    return "Context does not allow for an explicit (custom) " + descriptionOfThingBeingChecked;


            return null;
        }

        /// <inheritdoc/>
        public bool IsAllowable(IPipeline pipeline)
        {
            string whocares;
            return IsAllowable(pipeline, out whocares);
        }


        /// <summary>
        /// Initializes the given <paramref name="component"/> by calling all <see cref="IPipelineRequirement{T}"/> it implements with the appropriate
        /// input object <paramref name="parameters"/>
        /// </summary>
        /// <param name="listener"></param>
        /// <param name="component"></param>
        /// <param name="parameters"></param>
        public void PreInitialize(IDataLoadEventListener listener, IDataFlowComponent<T> component, params object[] parameters)
        {
            PreInitializeComponentWithAllObjects(listener, component, parameters);
        }

        /// <summary>
        /// Initializes the given <paramref name="component"/> by calling all <see cref="IPipelineRequirement{T}"/> it implements with the appropriate
        /// input object <paramref name="parameters"/>
        /// </summary>
        public void PreInitialize(IDataLoadEventListener listener, IDataFlowSource<T> component, params object[] parameters)
        {
            PreInitializeComponentWithAllObjects(listener, component, parameters);
        }

               
        private void PreInitializeComponentWithAllObjects(IDataLoadEventListener listener, object component, params object[] parameters)
        {
            //these are all the interfaces like IPipelineRequirement<TableInfo> etc
            var requirements = component.GetType().GetInterfaces().Where(i =>
                i.IsGenericType && 
                i.GetGenericTypeDefinition() 
                == typeof(IPipelineRequirement<>)).ToArray();

            Satisfy(requirements, false, listener, component,parameters);

            // satisfy optional requirements
            //these are all the interfaces like IPipelineOptionalRequirement<TableInfo> etc
            var optionals = component.GetType().GetInterfaces().Where(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition()
                == typeof(IPipelineOptionalRequirement<>)).ToArray();

            Satisfy(optionals, true, listener, component, parameters);
        }

        private void Satisfy(Type[] requirements, bool isOptional, IDataLoadEventListener listener, object component, params object[] parameters)
        {
            Dictionary<object, Dictionary<MethodInfo, object>> initializedComponents = new Dictionary<object, Dictionary<MethodInfo, object>>();

            //these are the T tokens in the above interfaces
            var typesRequired = requirements.Select(i => i.GenericTypeArguments[0]).ToList();
                   
            //now initialize all the parameters
            foreach (object parameter in parameters)
            {
                if (parameter == null)
                    throw new InvalidOperationException("One of the parameters for PreInitialization of " + component + " is null");

                //see if any of them are forbidden (e.g. if context was created without LoadsSingleTableInfo then it is forbidden to have components with IPipelineRequirement<TableInfo>)
                if (!IsAllowable(parameter.GetType()))
                    throw new Exception("Type " + GetFullName(parameter.GetType()) + " is not an allowable PreInitialize parameters type under the current DataFlowPipelineContext (check which flags you passed to the DataFlowPipelineContextFactory and the interfaces IPipelineRequirement<> that your components implement) ");

                var toRemove = PreInitializeComponentWithSingleObject(listener, component, parameter, initializedComponents);

                if (toRemove != null)
                    typesRequired.Remove(toRemove);
            }

            if (typesRequired.Any() && !isOptional)
                throw new Exception(
                    "Component '" + component.GetType().Name + "' reports a problem" + Environment.NewLine +
                    "The following expected types were not passed to PreInitialize:" + string.Join(",", typesRequired.Select(GetFullName))

                    + Environment.NewLine + "The object types passed were:" + Environment.NewLine +
                    string.Join(Environment.NewLine, parameters.Select(p => p.GetType() + ":" + p.ToString()))
                    );
        }

        private Type PreInitializeComponentWithSingleObject(IDataLoadEventListener listener, object component, object value, Dictionary<object, Dictionary<MethodInfo, object>> initializedComponents)
        {
            var compatibleInterfaces = component.GetType()
                .GetInterfaces().Where(i => 
                    i.IsGenericType && (i.GenericTypeArguments[0] == value.GetType() || i.GenericTypeArguments[0].IsInstanceOfType(value))
                    ).ToArray();

            if (compatibleInterfaces.Length == 0)
                return null;

            if(compatibleInterfaces.Length > 1)
                throw new OverlappingImplementationsException("The following IPipelineRequirement<> interfaces are implemented on pipeline component of type '" + component.GetType().Name + "' which are intercompatible with the input object of type '" + value.GetType().Name +"' "+ string.Join(",", compatibleInterfaces.Select(GetFullName)));

            var interfaceToInvokeIfAny = compatibleInterfaces[0];
            if (interfaceToInvokeIfAny != null)
            {
                //We have an interface that matches the input object, let's call it
                var preInit = interfaceToInvokeIfAny.GetMethod("PreInitialize");
                
                //but first document the fact that we have foundit
                if (!initializedComponents.ContainsKey(component))
                    initializedComponents.Add(component, new Dictionary<MethodInfo, object>());

                if (initializedComponents[component].ContainsKey(preInit))
                    throw new MultipleMatchingImplmentationException("Interface " + GetFullName(interfaceToInvokeIfAny) + " matches both input objects '" + initializedComponents[component][preInit] + "' ('" + initializedComponents[component][preInit].GetType().Name + "') and '" + value + "' ('" + value.GetType().Name + "')");
                else
                    initializedComponents[component].Add(preInit, value);
                
                //invoke it
                preInit.Invoke(component, new[] {value, listener});
                
                //return the type of T for IPipelineRequirement<T> interface that was called
                return interfaceToInvokeIfAny.GenericTypeArguments[0];
            }

            return null;
        }

        /// <inheritdoc/>
        public void PreInitializeGeneric(IDataLoadEventListener listener, object component, params object[] initializationObjects)
        {
            if(component is IDataFlowSource<T>)
                PreInitialize(listener, (IDataFlowSource<T>)component, initializationObjects);
            else if (component is IDataFlowComponent<T>)
                PreInitialize(listener, (IDataFlowComponent<T>) component, initializationObjects);
            else
                throw new NotSupportedException(
                    "It looks like you attempted to pre initialize using PreInitializeGeneric but your object was type '" +
                    GetFullName(component.GetType()) + "' and we expected either a source or a component <T> where <T> is:" +
                    GetFullName(typeof (T)));
        }

      
        private static string GetFullName(Type t)
        {
            if (!t.IsGenericType)
                return t.Name;
            StringBuilder sb = new StringBuilder();

            sb.Append(t.Name.Substring(0, t.Name.LastIndexOf("`")));
            sb.Append(t.GetGenericArguments().Aggregate("<",

                delegate(string aggregate, Type type)
                {
                    return aggregate + (aggregate == "<" ? "" : ",") + GetFullName(type);
                }
                ));
            sb.Append(">");

            return sb.ToString();
        }

        /// <inheritdoc/>
        public IEnumerable<Type> GetIPipelineRequirementsForType(Type t)
        {
            return t.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPipelineRequirement<>)).Select(r=>r.GetGenericArguments()[0]);
        }

        /// <inheritdoc/>
        public Type GetFlowType()
        {
            return typeof (T);
        }
    }
}
