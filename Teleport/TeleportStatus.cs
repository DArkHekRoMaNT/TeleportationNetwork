using System;
using Vintagestory.API.Datastructures;

namespace TeleportationNetwork
{
    public class TeleportStatus
    {
        public enum FSMState
        {
            Broken,
            Repairing,
            Activating,
            Activated,
            Deactivating,
            Deactivated
        }

        public delegate void StateChangeHandler(FSMState prev, FSMState next);

        public event StateChangeHandler? StateChanged;

        public FSMState State
        {
            get => _state;
            set
            {
                StateChanged?.Invoke(_state, value);
                _state = value;
            }
        }

        public bool IsBroken => _state == FSMState.Broken;

        public bool IsRepaired => _state != FSMState.Broken && _state != FSMState.Repairing;

        public float Progress => _timer / (_state == FSMState.Repairing ? Constants.TeleportRepairTime : Constants.TeleportActivationTime);

        private float _timer;
        private FSMState _state;

        public TeleportStatus()
        {
            _state = FSMState.Broken;
        }

        public void OnTick(float dt)
        {
            switch (State)
            {
                case FSMState.Broken:
                    _timer = 0;
                    break;

                case FSMState.Repairing:
                    _timer += dt;
                    if (_timer >= Constants.TeleportRepairTime)
                    {
                        State = FSMState.Deactivated;
                        _timer = 0;
                    }
                    break;

                case FSMState.Activating:
                    _timer += dt;
                    if (_timer >= Constants.TeleportActivationTime)
                    {
                        State = FSMState.Activated;
                    }
                    break;

                case FSMState.Activated:
                    _timer = Constants.TeleportActivationTime;
                    break;

                case FSMState.Deactivating:
                    _timer -= dt * 2;
                    if (_timer <= 0)
                    {
                        State = FSMState.Deactivated;
                    }
                    break;

                case FSMState.Deactivated:
                    _timer = 0;
                    break;
            }
            _timer = Math.Clamp(_timer, 0, Constants.TeleportActivationTime);
        }

        public void Start()
        {
            if (State == FSMState.Deactivated)
            {
                State = FSMState.Activating;
            }
        }

        public void Stop()
        {
            if (State == FSMState.Activated || State == FSMState.Activating)
            {
                State = FSMState.Deactivating;
            }
        }

        public void Repair()
        {
            if (State == FSMState.Broken)
            {
                State = FSMState.Repairing;
            }
        }

        public void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetFloat("activatorTime", _timer);
            tree.SetInt("activatorState", (int)_state);
        }

        public void FromTreeAttributes(ITreeAttribute tree)
        {
            _timer = tree.GetFloat("activatorTime");
            _state = (FSMState)tree.GetInt("activatorState");
        }
    }
}
