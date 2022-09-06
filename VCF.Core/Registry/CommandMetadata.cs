using System;
using System.Reflection;

namespace VampireCommandFramework.Registry;

internal record CommandMetadata(CommandAttribute Attribute, MethodInfo Method, ConstructorInfo Constructor, ParameterInfo[] Parameters, Type ContextType, Type ConstructorType, CommandGroupAttribute GroupAttribute);
