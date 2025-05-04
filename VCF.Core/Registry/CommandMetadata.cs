using System;
using System.Reflection;

namespace VampireCommandFramework.Registry;

internal record CommandMetadata(CommandAttribute Attribute, Assembly Assembly, MethodInfo Method, ConstructorInfo Constructor, ParameterInfo[] Parameters, Type ContextType, Type ConstructorType, CommandGroupAttribute GroupAttribute);
