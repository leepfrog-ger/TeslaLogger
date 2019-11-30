﻿namespace TeslaLogger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class Address
    {
        public Address(string name, double lat, double lng, int radius)
        {
            this.name = name;
            this.lat = lat;
            this.lng = lng;
            this.radius = radius;
        }

        public string name;
        public double lat;
        public double lng;
        public int radius;
    }

    class Geofence
    {
        List<Address> sortedList;
        System.IO.FileSystemWatcher fsw;

        public bool RacingMode = false;

        public Geofence()
        {
            Init();
            if (fsw == null)
            {
                fsw = new System.IO.FileSystemWatcher(FileManager.GetExecutingPath(), "*.csv");
                fsw.NotifyFilter = System.IO.NotifyFilters.LastWrite;
                fsw.Changed += Fsw_Changed;
                // fsw.Created += Fsw_Changed;
                // fsw.Renamed += Fsw_Changed;
                fsw.EnableRaisingEvents = true;
            }
        }

        void Init()
        {
            List<Address> list = new List<Address>();

            if (System.IO.File.Exists(FileManager.GetFilePath(TLFilename.GeofenceRacingFilename)) && ApplicationSettings.Default.RacingMode)
            {
                ReadGeofenceFile(list, FileManager.GetFilePath(TLFilename.GeofenceRacingFilename));
                RacingMode = true;

                Logfile.Log("*** RACING MODE ***");
            }
            else
            {
                RacingMode = false;
                ReadGeofenceFile(list, FileManager.GetFilePath(TLFilename.GeofenceFilename));
                if (!System.IO.File.Exists(FileManager.GetFilePath(TLFilename.GeofencePrivateFilename)))
                {
                    Logfile.Log("Create: " + FileManager.GetFilePath(TLFilename.GeofencePrivateFilename));
                    System.IO.File.AppendAllText(FileManager.GetFilePath(TLFilename.GeofencePrivateFilename), "");
                }

                UpdateTeslalogger.chmod(FileManager.GetFilePath(TLFilename.GeofencePrivateFilename), 666);
                ReadGeofenceFile(list, FileManager.GetFilePath(TLFilename.GeofencePrivateFilename));
            }
            
            Logfile.Log("Addresses inserted: " + list.Count);

            sortedList = list.OrderBy(o => o.lat).ToList();
        }

        private void Fsw_Changed(object sender, System.IO.FileSystemEventArgs e)
        {
            Logfile.Log("CSV File changed: " + e.Name);
            fsw.EnableRaisingEvents = false;

            System.Threading.Thread.Sleep(5000);
            Init();

            fsw.EnableRaisingEvents = true;
        }

        private static void ReadGeofenceFile(List<Address> list, string filename)
        {
            if (System.IO.File.Exists(filename))
            {
                Logfile.Log("Read Geofence File: " + filename);
                string line;
                using (System.IO.StreamReader file = new System.IO.StreamReader(filename))
                {
                    while ((line = file.ReadLine()) != null)
                    {
                        try
                        {
                            if (String.IsNullOrEmpty(line))
                                continue;

                            int radius = 50;

                            var args = line.Split(',');

                            if (args.Length > 3)
                            {
                                int.TryParse(args[3], out radius);
                            }

                            list.Add(new Address(args[0].Trim(),
                                Double.Parse(args[1].Trim(), Tools.ciEnUS.NumberFormat),
                                Double.Parse(args[2].Trim(), Tools.ciEnUS.NumberFormat),
                                radius));

                            if (!filename.Contains("geofence.csv"))
                                Logfile.Log("Address inserted: " + args[0]);
                        }
                        catch (Exception ex)
                        {
                            Logfile.ExceptionWriter(ex, line);
                        }
                    }
                }
            }
            else
            {
                Logfile.Log("FileNotFound: " + filename);
            }
        }

        public Address GetPOI(double lat, double lng, bool logDistance = true)
        {
            lock (sortedList)
            {
                double range = 0.2; // apprx 10km

                foreach (var p in sortedList)
                {
                    
                    if (p.lat - range > lat)
                        return null; // da die liste sortiert ist, kann nichts mehr kommen

                    if ((p.lat - range) < lat &&
                        lat < (p.lat + range) &&
                        (p.lng - range) < lng &&
                        lng < (p.lng + range))
                    {
                        double distance = GetDistance(lng, lat, p.lng, p.lat);
                        if (p.radius > distance)
                        {
                            if (logDistance)
                                Logfile.Log($"Distance: {distance} - Radius: {p.radius} - {p.name}");

                            return p;
                        }
                    }
                }
            }

            return null;
        }

        public double GetDistance(double longitude, double latitude, double otherLongitude, double otherLatitude)
        {
            var d1 = latitude * (Math.PI / 180.0);
            var num1 = longitude * (Math.PI / 180.0);
            var d2 = otherLatitude * (Math.PI / 180.0);
            var num2 = otherLongitude * (Math.PI / 180.0) - num1;
            var d3 = Math.Pow(Math.Sin((d2 - d1) / 2.0), 2.0) + Math.Cos(d1) * Math.Cos(d2) * Math.Pow(Math.Sin(num2 / 2.0), 2.0);

            return 6376500.0 * (2.0 * Math.Atan2(Math.Sqrt(d3), Math.Sqrt(1.0 - d3)));
        }
    }
}
