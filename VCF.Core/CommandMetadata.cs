using System;
using System.Reflection;

namespace VampireCommandFramework;

internal record CommandMetadata(CommandAttribute Attribute, MethodInfo Method, ConstructorInfo Constructor, ParameterInfo[] Parameters, Type ContextType, Type ConstructorType);
