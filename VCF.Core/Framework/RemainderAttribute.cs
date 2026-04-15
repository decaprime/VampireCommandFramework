using System;

namespace VampireCommandFramework;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class RemainderAttribute : Attribute { }
