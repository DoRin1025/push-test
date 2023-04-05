using System;

/// <summary>
/// <para>@author Timur Calmatui</para>
/// </summary>
public class MessagePayloadTooBigException : Exception
{
    public MessagePayloadTooBigException(string message)
        : base(message)
    {
    }
}