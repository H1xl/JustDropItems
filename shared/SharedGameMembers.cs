using RoR2.Orbs;

namespace H1xl.Shared
{
    /// <summary>
    /// Game members reached by name from the shared code (<see cref="GameMember"/>). Link this file into every mod that
    /// links one of the files listed here; the level-1 test checks it like a mod's GameMembers.
    /// </summary>
    internal static class SharedGameMembers
    {
        // Orbs/ItemFlight.cs: the game's pickup orb effect, loaded by the game at startup
        internal static readonly GameField PickupTransferOrb_orbEffectPrefab = new GameField(typeof(PickupTransferOrb), "orbEffectPrefab");

        // Skins/SkinPhysics.cs: DynamicBone stepped by hand after posing
        internal static readonly GameMethod DynamicBone_SetupParticles =
            new GameMethod(typeof(DynamicBone), "SetupParticles");
        internal static readonly GameMethod DynamicBone_InitTransforms =
            new GameMethod(typeof(DynamicBone), "InitTransforms");
        internal static readonly GameMethod DynamicBone_ResetParticlesPosition =
            new GameMethod(typeof(DynamicBone), "ResetParticlesPosition");
        internal static readonly GameMethod DynamicBone_UpdateDynamicBones =
            new GameMethod(typeof(DynamicBone), "UpdateDynamicBones", typeof(float));
    }
}
