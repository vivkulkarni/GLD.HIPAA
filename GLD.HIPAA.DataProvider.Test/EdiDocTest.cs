﻿using System;
using System.IO;
using System.Linq;
using GLD.HIPAA.Doc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GLD.HIPAA.DataProvider.Test
{
    public class Person
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; }
    }

    [TestClass]
    public class EdiDocTest
    {
        private const string MongodbHost = "mongodb://localhost:27017";
        private string _docId = "X12_05010_835";

        /// <summary>
        ///     It does not use MongoProvider! oly the direct access to MongoDb
        /// </summary>
        [TestMethod]
        public void FillInDb()
        {
            const string dbName = "EDI";
            const string segmentCollection = "SegmentsTest";
            // started from a simple class.
            var client = new MongoClient(MongodbHost);
            MongoServer server = client.GetServer();
            MongoDatabase database = server.GetDatabase(dbName);
            MongoCollection<Segment> collection = database.GetCollection<Segment>(segmentCollection);

            collection.RemoveAll();

            string[] segments = GetSegmentsFromFile();

            int i = 0;
            foreach (string segment in segments)
                collection.Insert(new Segment(_docId, i++, segment));

            IOrderedEnumerable<Segment> sortedSegments = collection.FindAll().OrderBy(c => c.SegmentIndex);
            foreach (Segment segment in sortedSegments)
                Console.WriteLine("[{0}]\t{1}\t{2}", segment.Id, segment.Tag, segment.Value);

            Assert.IsNotNull(collection);
        }

        [TestMethod]
        public void MongoSegment()
        {
            string[] segments = GetSegmentsFromFile();

            var client = new MyMongo(MongodbHost);
            string segmentId = _docId + ".0";
            client.RemoveSegment(segmentId); // clean up!

            client.WriteSegment(new Segment(_docId, 0, segments[0]));

            string segmentFromDb = client.ReadSegment(segmentId);
            Assert.IsNotNull(segmentFromDb);
            Assert.AreEqual(segments[0], segmentFromDb);

            client.RemoveSegment(segmentId); // clean up!
        }

        [TestMethod]
        public void MongoReadDocumentAsWhole()
        {
            string docId = Guid.NewGuid().ToString();
            ReadFileAndWriteItInMongo(docId);
            CleanUpMongo(docId);
        }

        [TestMethod]
        public void FileToMongoToFileTrip()
        {
            // read test file
            // write it into MongoBd
            string docId = Guid.NewGuid().ToString();
            ReadFileAndWriteItInMongo(docId);

            // Read it back 
            string[] segments = ReadDocSegmentsFromMongo(docId);
            CleanUpMongo(docId);

            // and write into a file.
            const string targetFilePath = @"C:\Projects\GLD\HIPAA\GLD.HIPAA\GLD.HIPAA.Doc\Samples\GeneratedByTest.Sample.txt";

            if (File.Exists(targetFilePath))
                File.Delete(targetFilePath);

            MyFile.Write(targetFilePath, docId, segments);

            Assert.IsTrue(File.Exists(targetFilePath));
            Assert.IsTrue(File.ReadAllLines(targetFilePath).Length > 1);
        }

        private string[] GetSegmentsFromFile()
        {
            const string fileName = @"C:\Projects\GLD\HIPAA\GLD.HIPAA\GLD.HIPAA.Doc\Samples\835.Sample.txt";
            return File.ReadAllLines(fileName);
        }

        private void ReadFileAndWriteItInMongo(string docId)
        {
            string[] segmentValues = GetSegmentsFromFile();

            var segments = new Segment[segmentValues.Length];
            for (var i = 0; i < segmentValues.Length; i++)
                segments[i] = new Segment {DocumentId = docId, SegmentIndex = i, Tag = "Tag", Value = segmentValues[i]};

            var client = new MyMongo(MongodbHost);
            CleanUpMongo(docId);

            var doc = new Document {Id = docId, Type = "835"};
            client.WriteDocAndSegments(doc, segments);

            ReadDocSegmentsFromMongo(docId);
        }

        private static void CleanUpMongo(string docId)
        {
            var client = new MyMongo(MongodbHost);
            client.RemoveDocWithSegments(docId); // clean up!
        }

        private string[] ReadDocSegmentsFromMongo(string docId)
        {
            var client = new MyMongo(MongodbHost);
            string[] segmentFromDb = client.ReadDocumentAsWhole(docId);
            Assert.IsNotNull(segmentFromDb);
            Assert.IsTrue(segmentFromDb.Length > 1);
            foreach (string t in segmentFromDb)
                Assert.IsTrue(t.Length > 1);

            return segmentFromDb;
        }
    }
}