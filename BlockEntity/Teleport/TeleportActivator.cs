using System;
using Vintagestory.API.Datastructures;

namespace TeleportationNetwork
{
    public class TeleportActivator
    {
        public enum FSMState
        {
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

        public float Progress => _timer / Constants.TeleportActivationTime;

        private float _timer;
        private FSMState _state;

        public void OnTick(float dt)
        {
            switch (State)
            {
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
                    _timer -= dt;
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
