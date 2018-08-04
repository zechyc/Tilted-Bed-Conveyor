using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using System.Threading.Tasks;
using System.Globalization;

/*
 *  Step #1, shift all X to {parameter one} as origin
 *  Step #2, shift all Y to {parameter two} as origin
 *  Step #3, add forward offset to X value
 *  Step #4, adjust Z value to correct for hyp
 * 
 */

namespace GCodeShifter
{

    class Program
    {

        static double Angle = 35.0;
        static double Hyp = 0.0;
        static double Adj = 0.0;

        static double x_original = 0;
        static double y_original = 0;
        static double y_offset;
        static double currentOffset = 0.0;
        static double moveforward = 0;        

        static void Main(string[] args)
        {

            string inputFile = args[0];
            string tempFile =  inputFile.Substring(0, inputFile.LastIndexOf("\\")) + "\\"+ "_temp.gcode";
            string outputFile = args[1];
            string xoffsetLength = "";
            string yoffsetLength = "";
            string newAngle = "";
            string Slicer = "";

            //Make a temp file to work with. This means I can ovewrrite the original
            File.Delete(tempFile);
            File.Copy(inputFile, tempFile);
           
            // if we have an X offset, record it
            if (args.Length > 2)
            {
                xoffsetLength = args[2];
                Double.TryParse(xoffsetLength, out x_original);
            };

            // if we have a Y offset, record it
            if (args.Length > 3)
            {
                yoffsetLength = args[3];
                Double.TryParse(yoffsetLength, out y_original);
            }

            // if we have a angle, record it
           // if (args.Length > 4)
           // {
           //     string newHyp = args[5];
           //     Double.TryParse(newHyp, out Hyp);
           // }

            // if we have a angle, record it
            if (args.Length > 4)
            {
                string newAdj = args[4];
                Double.TryParse(newAdj, out Angle);
            }

            // calculate triangle sides
            
            Hyp = 1 / System.Math.Cos((90-Angle)/180*Math.PI);
            Adj = System.Math.Tan((90-Angle)/180*Math.PI);

            //Read slicer engine             
            using (StreamReader sr = File.OpenText(tempFile))
                {
                    using (StreamWriter sw = new StreamWriter(outputFile))
                    {
                        string s = String.Empty;
                        while ((s = sr.ReadLine()) != null && (Slicer==""))
                        {
                            if (s.IndexOf("Cura_SteamEngine") > 0) //Cura
                            {
                                Slicer="Cura";
                            }
                            if (s.IndexOf("Simplify3D(R)") > 0) //Cura
                            {
                                Slicer = "S3D";
                            }
                            if (s.IndexOf("Slic3r") > 0) //Cura
                            {
                                Slicer = "Slic3r";
                            }
                        }
                    }
                }
            
            //Process file
            try
            {

                using (StreamReader sr = File.OpenText(tempFile))
                {
                    using (StreamWriter sw = new StreamWriter(outputFile))
                    {
                        string s = String.Empty;
                        while ((s = sr.ReadLine()) != null)
                        {
                            sw.WriteLine(ProcessLine(s.TrimStart(), sw, Slicer));
                        }
                    }
                }
            }


           

            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
            }
            // close files
            //delete temp file
            File.Delete(tempFile);
            Console.WriteLine("GCodeShifter Complete");
            
            //}
        }

        static string ProcessLine(string lineData, StreamWriter sw, string slicer)
        {
            string[] temp;
            temp = lineData.Split(Char.Parse(" "));

            StringBuilder tempData = new StringBuilder(temp.Length);

            // if the first parameter is a G0 with a trailing Z
            // then this is a Z height change
            // we need to multiply the layer height by the Square Root of Two
            // and record the layer height, as it will be the offset going forward
            if (temp[0] == "G0" && (lineData.IndexOf("Z") > 0) && (slicer=="Cura")) //Cura
            {

                double currentZ = double.Parse(lineData.Substring(lineData.IndexOf("Z") + 1, (lineData.Length - lineData.IndexOf("Z") - 1)));

                 if (currentOffset == 0.0)
                {
                    // capture the very first layer height (as this is the X offset going forward.)
                    currentOffset = currentZ * Adj;  // .7 is the adjacent side length of a 35 degree angle
                }                

                lineData = lineData.Substring(0, lineData.IndexOf("Z") + 1) + (currentZ* Hyp).ToString();
                temp = lineData.Split(Char.Parse(" "));

                // and remember to add the new Y offset
                y_offset = currentZ *Adj - currentOffset; //y_offset + currentOffset; 
                                
            }
            //Slic3r
            if (temp[0] == "G1" && (lineData.IndexOf("Z") > 0) && (slicer == "Slic3r")) //S3D
            {

                double currentZ = double.Parse(lineData.Substring(lineData.IndexOf("Z") + 1, (lineData.Length - lineData.IndexOf("F") - lineData.IndexOf("Z")-2)));

                if (currentOffset == 0.0)
                {
                    // capture the very first layer height (as this is the Y offset going forward.)
                    currentOffset = currentZ * Adj;  // .7 is the adjacent side length of a 35 degree angle
                }

                lineData = lineData.Substring(0, lineData.IndexOf("Z") + 1) + (currentZ * Hyp).ToString();
                temp = lineData.Split(Char.Parse(" "));

                // and remember to add the new Y offset
                y_offset = currentZ * Adj - currentOffset; //y_offset + currentOffset; 

            }
            //S3D
            if (temp[0] == "G1" && (lineData.IndexOf("Z") > 0) && (slicer == "S3D")) //S3D
            {

                double currentZ = double.Parse(lineData.Substring(lineData.IndexOf("Z") + 1, (lineData.Length - lineData.IndexOf("F"))));

                if (currentOffset == 0.0)
                {
                    // capture the very first layer height (as this is the Y offset going forward.)
                    currentOffset = currentZ * Adj;  // .7 is the adjacent side length of a 35 degree angle
                }

                lineData = lineData.Substring(0, lineData.IndexOf("Z") + 1) + (currentZ * Hyp).ToString();
                temp = lineData.Split(Char.Parse(" "));

                // and remember to add the new Y offset
                y_offset = currentZ * Adj - currentOffset; //y_offset + currentOffset; 

            }


            if (currentOffset != 0.0)
            {

                // if we are on a G0 or G1 line (no Z!)
                if ((temp[0] == "G0" || temp[0] == "G1"))
                {
                    if (lineData.IndexOf("X") > 0)
                    {
                        if (lineData.IndexOf("Y") > 0)
                        {
                            bool xFixed = false;
                            bool yFixed = false;

                            for (int segment = 0; segment < temp.Length; segment++)
                            {
                                if (temp[segment].StartsWith("X") && !xFixed)
                                {
                                    double xValue = double.Parse(temp[segment].Substring(1));
                                    temp[segment] = "X" + (xValue + x_original).ToString();
                                    xFixed = !xFixed; 
                                }

                                if (temp[segment].StartsWith("Y") && !yFixed)
                                {
                                    
                                    //Find moveforward value
                                    if (moveforward == 0) { moveforward = double.Parse(temp[segment].Substring(1)); }
                                    double yValue = double.Parse(temp[segment].Substring(1));
                                    temp[segment] = "Y" + (yValue + y_offset + y_original - moveforward).ToString();
                                    xFixed = !xFixed;
                                }
                            }

                            lineData = "";
                            foreach (string segment in temp)
                            {
                                lineData = lineData + segment + " ";
                            }

                        }
                    }
                }
            }
            return lineData;
        }
    }
}
