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
    class HandIsolation : Process
    {
        public delegate void ProcessReadyHandler();
        public event ProcessReadyHandler ProcessReady;

        private Processing.ImageReadyHandler m_isoImageHandler;
        private static HashSet<int> m_validPixels;
        private static bool m_isInitialized;
        private Semaphore m_validPixelSemaphore;
        private enum DIRECTION { Right, Left, Up, Down };

        /// <summary>
        /// Empty constructor
        /// </summary>
        public HandIsolation()
        { m_validPixelSemaphore = new Semaphore(0, 1); }

        /// <summary>
        /// First populates the bit array for values then sets up the event listener
        /// </summary>
        /// <param name="p_toInit">The initialization frame</param>
        public void initialize(Image p_toInit)
        {
            ImageProcessing.findEdges(p_toInit);
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

            //expandSelection(p_toInit);
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
        /// Run vertically and horizantally from center until meeting an edge
        /// </summary>
        /// <param name="p_image"></param>
        private void expandSelection(Image p_image)
        {
            byte[] buffer;
            BitmapData data = lockBitmap(out buffer, ref p_image);

            //Create the points
            Point topLeft = new Point((p_image.Width / 2) - 50, (p_image.Height / 2 - 50));
            Point topRight = new Point((p_image.Width / 2) + 50, (p_image.Height / 2 - 50));
            Point bottomLeft = new Point((p_image.Width / 2) - 50, (p_image.Height / 2 + 50));
            Point bottomRight = new Point((p_image.Width / 2) + 50, (p_image.Height / 2 + 50));

            Parallel.Invoke(
                () =>
                {
                    //going up
                    dividedExpandSelectionRGB(buffer, topLeft, topRight, DIRECTION.Up, data.Width);
                },
                () =>
                {
                    //Right
                    dividedExpandSelectionRGB(buffer, topRight, bottomRight, DIRECTION.Right, data.Width);
                },
                () =>
                {
                    //Down
                    dividedExpandSelectionRGB(buffer, bottomLeft, bottomRight, DIRECTION.Down, data.Width);
                },
                () =>
                {
                    //Left
                    dividedExpandSelectionRGB(buffer, topLeft, bottomLeft, DIRECTION.Left, data.Width);
                });

            unlockBitmap(ref buffer, ref data, ref p_image);
        }

        /// <summary>
        /// Expands the valid pixels by going to the left and using a simple step edge detector
        ///  will stop upon reaching an edge.
        /// </summary>
        /// <param name="start">Point for starting always Left to right, top to bottom</param>
        /// <param name="end">Point to end</param>
        /// <param name="p_direction">flag for control</param>
        /// <param name="p_width">width of the image in pixels. Used for offset</param>
        private void dividedExpandSelectionRGB(byte[] p_buffer, Point start, Point end, DIRECTION p_direction, int p_width)
        {
            
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
                byte[] buffer;
                BitmapData data = lockBitmap(out buffer, ref p_image);

                //This method returns bit per pixel, we need bytes.
                int depth = Bitmap.GetPixelFormatSize(data.PixelFormat) / 8; 

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

                unlockBitmap(ref buffer, ref data, ref p_image);

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
