﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MotionGestureProcessing
{
    class HandIsolation
    {
        public delegate void ProcessReadyHandler();
        public event ProcessReadyHandler ProcessReady;

        private Processing.ImageReadyHandler m_isoImageHandler;
        private static HashSet<int> m_validPixels;
        private static bool m_isInitialized;

        /// <summary>
        /// Empty constructor
        /// </summary>
        public HandIsolation()
        {}

        /// <summary>
        /// First populates the bit array for values then sets up the event listener
        /// </summary>
        /// <param name="p_toInit">The initialization frame</param>
        public void initialize(Image p_toInit)
        {
            m_isInitialized = false;
            populateValidPixels(p_toInit);
            m_isInitialized = true;
            
            setupListener();
            doWork(p_toInit);
        }

        /// <summary>
        /// Will populate the bitArray
        /// </summary>
        /// <param name="p_toInit">Image to scan</param>
        private void populateValidPixels(Image p_toInit)
        {
            //Clear out an old but array
            if (m_validPixels != null)
                m_validPixels = null;

            m_validPixels = new HashSet<int>();

            //Take a small window and set valid hand pixels
            //TODO i need to come up with a spreading algorithm to get more hand pixels
            int height = ((Bitmap)p_toInit).Height;
            int width = ((Bitmap)p_toInit).Width;
            Rectangle window = new Rectangle((width / 2 - 50), (height / 2 - 50), 100, 100);

            for (int y = window.Top; y < window.Bottom; ++y)
                 for (int x = window.Left; x < window.Right; ++x)
                {
                    Color color = ((Bitmap)p_toInit).GetPixel(x, y);
                    m_validPixels.Add(color.ToArgb());
                }
        }

        /// <summary>
        /// Establishes a listening connection 
        /// </summary>
        private void setupListener()
        {
            m_isoImageHandler = (obj, image) =>
            {
                this.doWork(image);
            };

            Processing.getInstance().IsolationImageFilled += m_isoImageHandler;
        }

        /// <summary>
        /// Used to pass in data to dividedDoWork for multithreaded doWork
        /// </summary>
        class workerData : Object
        {
            public Image m_imagePtr;
            public int m_quadrant;

            public workerData(Image p_image, int p_quadrant)
            {
                m_imagePtr = p_image;
                m_quadrant = p_quadrant;
            }
        }

        /// <summary>
        /// this method transforms the image into a filtered image
        /// UPDATE: this now performs almost insantly instead of the 2 seconds it took before
        /// </summary>
        /// <param name="p_image"></param>
        private void doWork(Image p_image)
        {
            if (m_isInitialized)
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
                var buffer = new Byte[data.Width * data.Height * depth];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                #region Call Parallel.Invoke for each coordinate
                //Only want to do ARGB and RGB check one time
                // Creates more code but is faster
                if (depth == 3)
                    Parallel.Invoke(
                        () =>
                        {
                            //upper left
                            dividedDoWorkRGB(buffer, 0, 0, data.Width / 2, data.Height / 2, data.Width);
                        },
                        () =>
                        {
                            //upper right
                            dividedDoWorkRGB(buffer, data.Width / 2, 0, data.Width, data.Height / 2, data.Width);
                        },
                        () =>
                        {
                            //lower left
                            dividedDoWorkRGB(buffer, 0, data.Height / 2, data.Width / 2, data.Height, data.Width);
                        },
                        () =>
                        {
                            //lower right
                            dividedDoWorkRGB(buffer, data.Width / 2, data.Height / 2, data.Width, data.Height, data.Width);
                        });
                else
                    Parallel.Invoke(
                        () =>
                        {
                            //upper left
                            dividedDoWorkARGB(buffer, 0, 0, data.Width / 2, data.Height / 2, data.Width);
                        },
                        () =>
                        {
                            //upper right
                            dividedDoWorkARGB(buffer, data.Width / 2, 0, data.Width, data.Height / 2, data.Width);
                        },
                        () =>
                        {
                            //lower left
                            dividedDoWorkARGB(buffer, 0, data.Height / 2, data.Width / 2, data.Height, data.Width);
                        },
                        () =>
                        {
                            //lower right
                            dividedDoWorkARGB(buffer, data.Width / 2, data.Height / 2, data.Width, data.Height, data.Width);
                        });
                #endregion

                //Copy it back and fill the image with the modified data
                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
                ((Bitmap)p_image).UnlockBits(data);

                Processing.getInstance().ToPCAImage = p_image;

                //If someone is listener raise an event
                if (ProcessReady != null)
                    ProcessReady();
            }
        }

        /// <summary>
        /// Handles comparing pixels to the valid pixels for RGB or 24bpp
        /// </summary>
        /// <param name="p_buffer">Byte array of image to process</param>
        /// <param name="startX">Start X position (0 = left)</param>
        /// <param name="startY">Start Y position (0 = top)</param>
        /// <param name="endX"></param>
        /// <param name="endY"></param>
        /// <param name="width">Width in pixels used to determine offset</param>
        private void dividedDoWorkRGB(byte[] p_buffer, int startX, int startY, int endX, int endY, int width)
        {
            //Constant to be used in filling in the non valid points.
            Color BLACK = Color.Black;

            //To be overwritten 
            int offset;
            int curPixelColor;
            
            for (int y = startY; y < endY; ++y)
                for (int x = startX; x < endX; ++x)
                { 
                    //Just a basic transform from 1 dimension to 2
                    offset = ((y * width) + x) * 3;
                    
                    //FromArgb requires rgb but array is ordered bgr
                    curPixelColor = Color.FromArgb(p_buffer[offset + 2], p_buffer[offset + 1], p_buffer[offset]).ToArgb();
                    if (!m_validPixels.Contains(curPixelColor))
                    {
                        p_buffer[offset + 0] = BLACK.B;
                        p_buffer[offset + 1] = BLACK.G;
                        p_buffer[offset + 2] = BLACK.R;
                    }
                }
        }

        /// <summary>
        /// Handles comparing pixels to the valid pixels for ARGB or 32bpp
        /// This is an exact copy of dividedDoWorkRGB except the get and replace functions include space for ARGB format
        /// </summary>
        /// <param name="p_buffer">Byte array of image to process</param>
        /// <param name="startX">Start X position (0 = left)</param>
        /// <param name="startY">Start Y position (0 = top)</param>
        /// <param name="endX"></param>
        /// <param name="endY"></param>
        /// <param name="width">Width in pixels used to determine offset</param>
        private void dividedDoWorkARGB(byte[] p_buffer, int startX, int startY, int endX, int endY, int width)
        {
            //Read above comments.
            Color BLACK = Color.Black;

            int offset;
            int curPixelColor;
            
            for (int y = startY; y < endY; ++y)
                for (int x = startX; x < endX; ++x)
                {
                    offset = ((y * width) + x) * 4;
                    curPixelColor = Color.FromArgb(p_buffer[offset + 3], p_buffer[offset + 2], 
                                                   p_buffer[offset + 1], p_buffer[offset]).ToArgb();
                    if (!m_validPixels.Contains(curPixelColor))
                    {
                        p_buffer[offset + 0] = BLACK.B;
                        p_buffer[offset + 1] = BLACK.G;
                        p_buffer[offset + 2] = BLACK.R;
                        p_buffer[offset + 3] = BLACK.A;
                    }
                }
        }
    }
}