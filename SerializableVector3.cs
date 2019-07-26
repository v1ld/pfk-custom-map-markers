// By Cherno, from http://answers.unity.com/answers/956580/view.html

using System;
using System.Runtime.Serialization;
using UnityEngine;

/// Since unity doesn't flag the Vector3 as serializable, we
/// need to create our own version. This one will automatically convert
/// between Vector3 and SerializableVector3
[DataContract]
public struct SerializableVector3
{
    [DataMember]
    public float x;
    [DataMember]
    public float y;
    [DataMember]
    public float z;

    public SerializableVector3(float rX, float rY, float rZ)
    {
        x = rX;
        y = rY;
        z = rZ;
    }

    public override string ToString()
    {
        return String.Format("[{0}, {1}, {2}]", x, y, z);
    }

    public static implicit operator Vector3(SerializableVector3 rValue)
    {
        return new Vector3(rValue.x, rValue.y, rValue.z);
    }

    public static implicit operator SerializableVector3(Vector3 rValue)
    {
        return new SerializableVector3(rValue.x, rValue.y, rValue.z);
    }
}
