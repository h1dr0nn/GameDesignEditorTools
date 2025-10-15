#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEngine;

public static class AudioWavUtility
{
    public static byte[] Encode(float[] samples, int channels, int sampleRate)
    {
        int sampleCount = samples.Length;
        short[] pcm = new short[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            pcm[i] = (short)Mathf.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);

        int subChunk2 = pcm.Length * 2;
        int chunkSize = 36 + subChunk2;
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms, Encoding.ASCII))
        {
            bw.Write(Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(chunkSize);
            bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt "));
            bw.Write(16);
            bw.Write((short)1);
            bw.Write((short)channels);
            bw.Write(sampleRate);
            bw.Write(sampleRate * channels * 2);
            bw.Write((short)(channels * 2));
            bw.Write((short)16);
            bw.Write(Encoding.ASCII.GetBytes("data"));
            bw.Write(subChunk2);
            foreach (short s in pcm) bw.Write(s);
            return ms.ToArray();
        }
    }
}
#endif
