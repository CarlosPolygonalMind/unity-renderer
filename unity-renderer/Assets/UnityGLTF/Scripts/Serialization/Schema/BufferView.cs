﻿using GLTF.Utilities;
using Newtonsoft.Json;

namespace GLTF.Schema
{
    public enum BufferViewTarget
    {
        None = 0,
        ArrayBuffer = 34962,
        ElementArrayBuffer = 34963,
    }

    /// <summary>
    /// A view into a buffer generally representing a subset of the buffer.
    /// </summary>
    public class BufferView : GLTFChildOfRootProperty
    {
        /// <summary>
        /// The index of the buffer.
        /// </summary>
        public BufferId Buffer;

        /// <summary>
        /// The offset into the buffer in bytes.
        /// <minimum>0</minimum>
        /// </summary>
        public uint ByteOffset;

        /// <summary>
        /// The length of the bufferView in bytes.
        /// <minimum>0</minimum>
        /// </summary>
        public uint ByteLength;

        /// <summary>
        /// The stride, in bytes, between vertex attributes or other interleavable data.
        /// When this is zero, data is tightly packed.
        /// <minimum>0</minimum>
        /// <maximum>255</maximum>
        /// </summary>
        public uint ByteStride;

        /// <summary>
        /// The target that the WebGL buffer should be bound to.
        /// All valid values correspond to WebGL enums.
        /// When this is not provided, the bufferView contains animation or skin data.
        /// </summary>
        public BufferViewTarget Target = BufferViewTarget.None;

        public BufferView()
        {
        }

        public BufferView(BufferView bufferView, GLTFRoot gltfRoot) : base(bufferView, gltfRoot)
        {
            Buffer = new BufferId(bufferView.Buffer, gltfRoot);
            ByteOffset = bufferView.ByteOffset;
            ByteLength = bufferView.ByteLength;
            ByteStride = bufferView.ByteStride;
            Target = bufferView.Target;
        }

        public static BufferView Deserialize(GLTFRoot root, JsonReader reader)
        {
            var bufferView = new BufferView();

            while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            {
                var curProp = reader.Value.ToString();

                switch (curProp)
                {
                    case "buffer":
                        bufferView.Buffer = BufferId.Deserialize(root, reader);
                        break;
                    case "byteOffset":
                        bufferView.ByteOffset = reader.ReadDoubleAsUInt32();
                        break;
                    case "byteLength":
                        bufferView.ByteLength = reader.ReadDoubleAsUInt32();
                        break;
                    case "byteStride":
                        bufferView.ByteStride = reader.ReadDoubleAsUInt32();
                        break;
                    case "target":
                        bufferView.Target = (BufferViewTarget)reader.ReadAsInt32().Value;
                        break;
                    default:
                        bufferView.DefaultPropertyDeserializer(root, reader);
                        break;
                }
            }

            return bufferView;
        }

        public override void Serialize(JsonWriter writer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("buffer");
            writer.WriteValue(Buffer.Id);

            if (ByteOffset != 0)
            {
                writer.WritePropertyName("byteOffset");
                writer.WriteValue(ByteOffset);
            }

            writer.WritePropertyName("byteLength");
            writer.WriteValue(ByteLength);

            if (ByteStride != 0)
            {
                writer.WritePropertyName("byteStride");
                writer.WriteValue(ByteStride);
            }

            if (Target != BufferViewTarget.None)
            {
                writer.WritePropertyName("target");
                writer.WriteValue((int)Target);
            }

            base.Serialize(writer);

            writer.WriteEndObject();
        }
    }
}
