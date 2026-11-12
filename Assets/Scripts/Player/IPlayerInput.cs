using UnityEngine;

namespace CyberBiker.Player {
    public interface IPlayerInput {
        Vector2 Steering { get; }
        Vector2 AimDelta { get; }
        bool IsAiming { get; }
    }
}
