﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Collections;
using System.Collections.Generic;

using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Xna.Framework;


namespace onboard
{
    public class DevcadeClient
    {
        private string accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
        private string secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

        private string _bucketName = "devcade-games";

        private static AmazonS3Config _config;
        private static AmazonS3Client _s3Client;

        public DevcadeClient()
        {
            _config = new AmazonS3Config();
            _config.ServiceURL = "https://s3.csh.rit.edu";
            _config.ForcePathStyle = true;

            _s3Client = new AmazonS3Client(
                    accessKey,
                    secretKey,
                    _config
                    );

            ListBucketsResponse response = _s3Client.ListBucketsAsync().Result;
            foreach (S3Bucket b in response.Buckets)
            {
                Console.WriteLine("{0}\t{1}", b.BucketName, b.CreationDate);
            }

            List<string> bucketContents = ListBucketContentsAsync("devcade-games").Result;
                
            //TransferUtility fileTransferUtility = new TransferUtility(_s3Client);

            // Note the 'fileName' is the 'key' of the object in S3 (which is usually just the file name)
        //    fileTransferUtility.Download("/tmp/bankshot.zip", _bucketName, "bankshot.zip");
        }
        // Returns true if success and false otherwise
        // permissions can be an int or a string. For example it can also be +x, -x etc..
        bool Chmod(string filePath, string permissions = "700", bool recursive = false)
        {
                string cmd;
                if (recursive)
                    cmd = $"chmod -R {permissions} {filePath}";
                else
                    cmd = $"chmod {permissions} {filePath}";

                try
                {
                    using (Process proc = Process.Start("/bin/bash", $"-c \"{cmd}\""))
                    {
                        proc.WaitForExit();
                        return proc.ExitCode == 0;
                    }
                }
                catch
                {
                    return false;
                }
        }

        public void runGame(string game)
        {
            //string objectKey = "EMR" + "/" + imagename;
            //EMR is folder name of the image inside the bucket 
            //GetObjectRequest request = new GetObjectRequest();
            //request.BucketName = _bucketName;
            //request.Key = game;
            //request.Key = "bankshot.zip";
            //GetObjectResponse response = GetObjectAsync(request).Result;
            //WriteResponseStreamToFileAsync(response, "/tmp/" + game);
            
            TransferUtility fileTransferUtility = new TransferUtility(_s3Client);

            string path = "/tmp/" + game + ".zip";

            Console.WriteLine("Getting " + path);
            // Note the 'fileName' is the 'key' of the object in S3 (which is usually just the file name)
            fileTransferUtility.Download(path, _bucketName, game);

            try
            {
            Console.WriteLine("Extracting " + path);
            // Extract the specified path (the zip file) to the specified directory (/tmp/, probably)
            System.IO.Directory.CreateDirectory("/tmp/" + game);
            ZipFile.ExtractToDirectory(path, "/tmp/" + game);
            } catch (System.IO.IOException e) {
                Console.WriteLine(e);
            }

            Console.WriteLine("Running " + path);

            string execPath = "/tmp/" + game + "/publish/" + game;
            Chmod(execPath,"+x",false);

            Process process = new Process()
            {
                StartInfo = new ProcessStartInfo(execPath) // chom
                {
                    WindowStyle = ProcessWindowStyle.Normal,
                    WorkingDirectory = Path.GetDirectoryName(execPath)
                }
            };

            process.Start();
        }

        public void listBuckets()
        {
            Task<ListBucketsResponse> response = ListBucketsAsync();
            
            foreach (S3Bucket b in response.Result.Buckets)
            {
                Console.WriteLine("{0}\t{1}", b.BucketName, b.CreationDate);
            }
        }

        // Async method to get a list of Amazon S3 buckets.
        private async Task<ListBucketsResponse> ListBucketsAsync()
        {
            var response = await _s3Client.ListBucketsAsync();
            return response;
        }

        private async Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request)
        {
            Console.WriteLine("Making call to s3");
            var response = await _s3Client.GetObjectAsync(request);
            Console.WriteLine(response.ToString());
            return response;
        }

        private async void WriteResponseStreamToFileAsync(GetObjectResponse response, string game)
        {
            Console.WriteLine("Object Retrive Call complete. Writing to file...");
            string path = "/tmp/" + game;
            CancellationToken chom;
            await response.WriteResponseStreamToFileAsync(path, false, chom);

            //var chom = await _s3Client.GetObjectAsync(request);
           // return response;
        }
        
        /// <summary>
        /// Shows how to list the objects in an Amazon S3 bucket.
        /// </summary>
        /// <param name="bucketName">The name of the bucket for which to list
        /// the contents.</param>
        /// <returns>A boolean value indicating the success or failure of the
        /// copy operation.</returns>
        public async Task<List<string>> ListBucketContentsAsync(string bucketName)
        {
            try
            {

                List<string> myGameTitles = new List<String>();
                var request = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    MaxKeys = 5,
                };

                Console.WriteLine("--------------------------------------");
                Console.WriteLine($"Listing the contents of {bucketName}:");
                Console.WriteLine("--------------------------------------");

                var response = new ListObjectsV2Response();

                do
                {
                    response = await _s3Client.ListObjectsV2Async(request);

                    response.S3Objects
                        .ForEach(obj => Console.WriteLine($"{obj.Key,-35}{obj.LastModified.ToShortDateString(),10}{obj.Size,10}"));

                    response.S3Objects.ForEach(obj => myGameTitles.Add(obj.Key));

                    // If the response is truncated, set the request ContinuationToken
                    // from the NextContinuationToken property of the response.
                    request.ContinuationToken = response.NextContinuationToken;
                }
                while (response.IsTruncated);

                return myGameTitles;
            }
            catch (AmazonS3Exception ex)
            {
                Console.WriteLine($"Error encountered on server. Message:'{ex.Message}' getting list of objects.");
                return new List<String>();
            }
        }
    }
}