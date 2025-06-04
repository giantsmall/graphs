using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace Assets.Game.Scripts.Utility
{

    public class ProtectedValue
    {
        protected byte distortionFactor;
        protected byte RandomizeDistortionFactor()
        {
            distortionFactor = new System.Random().NextByte(1, byte.MaxValue);
            return distortionFactor;
        }
    }

    public class ProtectedUShort : ProtectedValue
    {
        public ProtectedUShort(ushort value = 0)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException("value");
            }
            this.Value = value;
        }

        public ProtectedUShort(ProtectedUShort pUshort)
        {
            this.Value = pUshort.Value;
        }

        public static implicit operator string(ProtectedUShort d) => $"{d.Value}";

        public static implicit operator ushort(ProtectedUShort d) => d.Value;
        public static ProtectedUShort operator +(ProtectedUShort a, ushort b) => new ProtectedUShort() { Value = (ushort)(a.Value + b) };
        public static ProtectedUShort operator -(ProtectedUShort a, ushort b) => new ProtectedUShort() { Value = (ushort)(a.Value - b) };
        public static ProtectedUShort operator +(ProtectedUShort a, int b) => new ProtectedUShort(){ Value = (ushort)(a.Value + b) };
        public static ProtectedUShort operator -(ProtectedUShort a, int b) => new ProtectedUShort(){ Value = (ushort)(a.Value - b) };

        protected ushort value;
        public ushort Value
        {
            get
            {
                return (ushort)(this.value - distortionFactor);
            }

            private set
            {
                this.value = (ushort)(value + RandomizeDistortionFactor());
            }
        }
    }

    public class ProtectedShort : ProtectedValue
    {
        public ProtectedShort(short value = 0)
        {
            this.Value = value;
        }

        public ProtectedShort(ProtectedShort pShort)
        {
            this.Value = pShort.Value; 
        }

        public static implicit operator string(ProtectedShort d) => $"{d.Value}";
        public static implicit operator short(ProtectedShort d) => d.Value;
        public static ProtectedShort operator +(ProtectedShort a, short b) => new ProtectedShort()  {  Value = (short)(a.Value + b)  };
        public static ProtectedShort operator -(ProtectedShort a, short b) => new ProtectedShort()  {  Value = (short)(a.Value - b)  };

        protected short value;
        public short Value
        {
            get
            {
                return (short)(this.value - distortionFactor);
            }

            private set
            {
                this.value = (short)(value + RandomizeDistortionFactor());
            }
        }
    }

    public class ProtectedInt : ProtectedValue
    {
        public ProtectedInt(int value = 0)
        {
            this.Value = value;
        }

        public ProtectedInt(ProtectedInt pInt)
        {
            this.Value = pInt.Value;
        }

        public static implicit operator string(ProtectedInt d) => $"{d.Value}";
        public static implicit operator int(ProtectedInt d) => d.Value;
        public static implicit operator ProtectedInt(int d) => new ProtectedInt(d);
        public static ProtectedInt operator +(ProtectedInt a, ushort b) => new ProtectedInt() { Value = (a.Value + b) };
        public static ProtectedInt operator -(ProtectedInt a, ushort b) => new ProtectedInt() { Value = (a.Value - b) };
        public static ProtectedInt operator +(ProtectedInt a, ProtectedInt b) => new ProtectedInt() { Value = (a.Value + b.value) };

        protected int value;
        public int Value
        {
            get
            {
                return this.value - distortionFactor;
            }

            private set
            {
                this.value = value + RandomizeDistortionFactor();
            }
        }
    }

    public class ProtectedFloat : ProtectedValue
    {
        public ProtectedFloat(float value = 0)
        {
            this.Value = value;
        }

        public static implicit operator string(ProtectedFloat d) => $"{d.Value}";
        public static implicit operator float(ProtectedFloat d) => d.Value;
        public static ProtectedFloat operator +(ProtectedFloat a, ushort b) => new ProtectedFloat() { Value = (a.Value + b) };
        public static ProtectedFloat operator -(ProtectedFloat a, ushort b) => new ProtectedFloat() { Value = (a.Value - b) };

        protected float value;
        public float Value
        {
            get
            {
                return this.value - distortionFactor;
            }

            private set
            {
                this.value = value + RandomizeDistortionFactor();
            }
        }
    }
}
