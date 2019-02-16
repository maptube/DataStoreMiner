using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DatastoreMiner
{
    /// <summary>
    /// Calculate a metric for how similar two images are. The images are assumed to be the same geographical region, so you shouldn't try matching a thumbnail image of Wales
    /// against an image of England, although you will get a low value.
    /// </summary>
    class ImageMatcher
    {
        /// <summary>
        /// Simple colour difference between each pixel with no geographical weighting.
        /// </summary>
        /// <param name="I1"></param>
        /// <param name="I2"></param>
        /// <returns>sqrt( (R1-R2)^2 + (G1-G2)^2 + (B1-B2)^2 ), summed over all pixels in both images</returns>
        public static double RGBDifference(Image I1, Image I2)
        {
            double dist = 0; //return value

            //annoying, have to draw images onto bitmaps with ARGB 32 bpp format. Maybe not? How fast is this?
            Bitmap bm1 = new Bitmap(I1.Width, I1.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bm1))
            {
                g.DrawImageUnscaled(I1, 0, 0);
            }
            Bitmap bm2 = new Bitmap(I1.Width, I1.Height, PixelFormat.Format32bppArgb); //yes, I1.Width&Height is correct!
            using (Graphics g = Graphics.FromImage(bm2))
            {
                g.DrawImageUnscaled(I2, 0, 0); //you might have to scale this in a better implementation, but I can guarantee I1 and I2 are the same dimension here
            }

            //now bm1 and bm2 contain images I1 and I2, go through every RGB value and measure distance
            BitmapData bmd1 = bm1.LockBits(new Rectangle(0, 0, bm1.Width, bm1.Height), ImageLockMode.ReadOnly, bm1.PixelFormat);
            BitmapData bmd2 = bm2.LockBits(new Rectangle(0, 0, bm2.Width, bm2.Height), ImageLockMode.ReadOnly, bm2.PixelFormat);
            try
            {
                // Blue, Green, Red, Alpha (Format32BppArgb)
                //int pixelSize = 4;
                //copy first image bytes into array
                IntPtr ptr1 = bmd1.Scan0;
                int bytes1  = Math.Abs(bmd1.Stride) * bm1.Height;
                byte[] rgb1 = new byte[bytes1];
                Marshal.Copy(ptr1, rgb1, 0, bytes1);
                //now copy second image bytes into array
                IntPtr ptr2 = bmd2.Scan0;
                int bytes2 = Math.Abs(bmd2.Stride) * bm2.Height; //This should be equal to bytes1
                byte[] rgb2 = new byte[bytes2];
                Marshal.Copy(ptr2, rgb2, 0, bytes2);

                //now go through all the RGB values and check distance in RGB space
                dist = 0;
                int count = 0;
                for (int i = 0; i < bytes1; i+=4)
                {
                    //blue, green, red, alpha
                    double dblue = (double)rgb1[i] - (double)rgb2[i];
                    double dgreen = (double)rgb1[i+1] - (double)rgb2[i+1];
                    double dred = (double)rgb1[i+2] - (double)rgb2[i+2];
                    dist += Math.Sqrt(dblue*dblue+dgreen*dgreen+dred*dred);
                    ++count;
                }
                dist /= count;
            }
            finally
            {
                bm1.UnlockBits(bmd1);
                bm2.UnlockBits(bmd2);
            }

            return dist;
        }

        /// <summary>
        /// C1=n/sumij(vij)
        /// C2=sumij(vij(xi-xbar)(yj-ybar)
        /// C3=sqrt(sumi( (xi-xbar)^2 )) sqrt(sumj( (yj-ybar)^2 ))
        /// Result = C1*C2/C3
        /// </summary>
        /// <param name="I1">Image 1</param>
        /// <param name="I2">Image 2</param>
        /// <returns></returns>
        public static double WartenbergCrossMoran(Image I1, Image I2)
        {
            //TODO: This is unfinished as a quick calculation shows that this is going to take about 17 minutes per image pair
            
            //both images should be the same size, so we're going to assume that they are
            int w = I1.Width;
            int h = I1.Height;

            //annoying, have to draw images onto bitmaps with ARGB 32 bpp format. Maybe not? How fast is this?
            Bitmap bm1 = new Bitmap(I1.Width, I1.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bm1))
            {
                g.DrawImageUnscaled(I1, 0, 0);
            }
            Bitmap bm2 = new Bitmap(I1.Width, I1.Height, PixelFormat.Format32bppArgb); //yes, I1.Width&Height is correct!
            using (Graphics g = Graphics.FromImage(bm2))
            {
                g.DrawImageUnscaled(I2, 0, 0); //you might have to scale this in a better implementation, but I can guarantee I1 and I2 are the same dimension here
            }

            //vij is weights matrix, but we're using distance
            //This is a 4 million loop and it must be possible to work this out using a formula, although you have to do the loop anyway so you could optimise it into the next bit
            double C1 = 0;
            for (int x1 = 0; x1<w; x1++)
            {
                for (int y1 = 0; y1 < h; y1++)
                {
                    for (int x2 = 0; x2<w; x2++)
                    {
                        for (int y2=0; y2<h; y2++)
                        {
                            //could use a number of distance measures here instead of Euclidean, including Manhattan, Mexican Hat etc
                            C1 += Math.Sqrt((x2 - x1) ^ 2 + (y2 - y1) ^ 2);
                        }
                    }
                }
            }
            C1 = w * h / C1; //where w*h=n in formula which is number of locations

            //calculate mean image intensities for green channel
            double xbar = 0, ybar = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    //get average pixel values for green channel only
                    xbar += bm1.GetPixel(x, y).G;
                    ybar += bm2.GetPixel(x, y).G;
                }
            }
            xbar /= w*h;
            ybar /= w*h;
            //xbar and ybar now contain average pixel intensities (green channel) for I1=xbar and I2=ybar

            return 0;
        }
    }
}
