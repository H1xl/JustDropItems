using System;
using System.Reflection;

namespace H1xl.Shared
{
    /// <summary>
    /// A member of the game (or of BepInEx) that a mod reaches by name: a hook target, a private field or property.
    /// Each mod declares all of them as static readonly fields of its <c>plugin/GameMembers.cs</c>; a test
    /// (GameMembersTests, run after game updates) loads the game's dlls and checks that every one still exists.
    /// <see cref="Find"/> never throws: a missing member gives null, and the feature using it turns off.
    /// </summary>
    internal abstract class GameMember
    {
        internal const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        /// <summary>Declaring type; null when it could not be found (see <see cref="Nested"/>).</summary>
        internal readonly Type Type;
        internal readonly string Name;

        protected GameMember(Type type, string name)
        {
            Type = type;
            Name = name;
        }

        /// <summary>The member, or null when the type or the member is gone (game updated?).</summary>
        internal abstract MemberInfo FindMember();

        /// <summary>A nested type that can't be named in code (private): null if it's gone.</summary>
        internal static Type Nested(Type outer, string name) => outer.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);

        public override string ToString() => $"{Type?.Name ?? "<missing type>"}.{Name}";
    }

    /// <summary>A method with exact parameter types (a hook target).</summary>
    internal sealed class GameMethod : GameMember
    {
        internal readonly Type[] Args;

        internal GameMethod(Type type, string name, params Type[] args) : base(type, name) => Args = args;

        internal MethodInfo Find()
        {
            try { return Type?.GetMethod(Name, Any, null, Args, null); }
            catch (Exception) { return null; }   // AmbiguousMatchException, a parameter type that fails to load
        }

        internal override MemberInfo FindMember() => Find();
    }

    internal sealed class GameField : GameMember
    {
        internal GameField(Type type, string name) : base(type, name) { }

        internal FieldInfo Find()
        {
            try { return Type?.GetField(Name, Any); }
            catch (Exception) { return null; }
        }

        internal override MemberInfo FindMember() => Find();
    }

    internal sealed class GameProperty : GameMember
    {
        internal GameProperty(Type type, string name) : base(type, name) { }

        internal PropertyInfo Find()
        {
            try { return Type?.GetProperty(Name, Any); }
            catch (Exception) { return null; }
        }

        internal override MemberInfo FindMember() => Find();
    }
}
