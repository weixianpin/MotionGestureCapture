﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotionGestureProcessing
{
    public class imageData
    {
        public bool InitialFrame { get; set; }
        public Image Image { get; set; }
        public List<Point> Datapoints { get; set; }
        public Rectangle Filter { get; set; }

        public imageData(bool p_isInit, Image p_image)
        {
            InitialFrame = p_isInit;
            Image = p_image;
        }
    }
}
