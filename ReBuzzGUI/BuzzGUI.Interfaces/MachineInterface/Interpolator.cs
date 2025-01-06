namespace Buzz.MachineInterface
{
    public class Interpolator
    {
        float value;
        float target;
        float delta;

        public Interpolator() { }
        public Interpolator(float v) { value = target = v; }

        public float Value
        {
            set
            {
                this.value = value;
            }
            get
            {
                return value;
            }
        }

        public void SetTarget(float t, int time)
        {
            target = t;

            if (time > 0)
            {
                delta = (target - value) / time;
            }
            else
            {
                delta = 0;
                value = target;
            }
        }

        public float Tick()
        {
            if (delta != 0.0f)
            {
                value += delta;

                if (delta > 0)
                {
                    if (value >= target)
                    {
                        value = target;
                        delta = 0;
                    }
                }
                else
                {
                    if (value <= target)
                    {
                        value = target;
                        delta = 0;
                    }
                }
            }

            return value;
        }
    }
}
