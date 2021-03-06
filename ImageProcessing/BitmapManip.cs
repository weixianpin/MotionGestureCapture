﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ImageProcessing
{
    public class BitmapManip
    {
        /// <summary>
        /// performs Bitmap.lockBits but does all the setup as well
        /// </summary>
        /// <param name="p_buffer">buffer to write out to</param>
        /// <param name="p_image">Image to write</param>
        /// <returns></returns>
        public static BitmapData lockBitmap(out byte[] p_buffer, Image p_image)
        {
            //Setting up a buffer to be used for concurrent read/write
            int width = ((Bitmap)p_image).Width;
            int height = ((Bitmap)p_image).Height;
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData data = ((Bitmap)p_image).LockBits(rect, ImageLockMode.ReadWrite,
                                                         ((Bitmap)p_image).PixelFormat);
            //This method returns bit per pixel, we need bytes.
            int depth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8;

            //Create a buffer to host the image data and copy the data in
            p_buffer = new Byte[data.Width * data.Height * depth];
            Marshal.Copy(data.Scan0, p_buffer, 0, p_buffer.Length);

            return data;
        }

        /// <summary>
        /// Unlocks the image, created this just for conformities sake
        /// </summary>
        /// <param name="p_buffer"></param>
        /// <param name="p_data"></param>
        /// <param name="p_image"></param>
        public static void unlockBitmap(ref byte[] p_buffer, ref BitmapData p_data, Image p_image)
        {
            //Copy it back and fill the image with the modified data
            Marshal.Copy(p_buffer, 0, p_data.Scan0, p_buffer.Length);
            ((Bitmap)p_image).UnlockBits(p_data);
        }
    }
}
