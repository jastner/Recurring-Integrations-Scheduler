using Ionic.Zip;
using RecurringIntegrationsScheduler.Common.Contracts;
using RecurringIntegrationsScheduler.Common.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace VendorInvoice_PackageCreation
{
    public static class PackageCreation
    {
        public static DataMessage xmlTransformation(DataMessage dataMessage, string uploadSuccessFilePath, string xsltHeaderFileName, string xsltLineFileName, string xsltAttachmentFileName, string manifestXMLFileName, string packageXMLFileName)
        {
            var sourceStream = FileOperationsHelper.Read(dataMessage.FullPath);
            if (sourceStream != null)
            {
                //If we need to "wrap" file in package envelope
                if (!String.IsNullOrEmpty(dataMessage.FullPath))
                {
                    // Find file name of input file xml
                    string fileName = Path.GetFileNameWithoutExtension(dataMessage.FullPath);                    
                    string tempOutputZipDirectory = $"{Path.GetTempPath()}\\{fileName}";
                    Directory.CreateDirectory(tempOutputZipDirectory);

                    // Create 3 xmls with XSLT
                    XPathDocument myXPathDoc = new XPathDocument(sourceStream);
                    XslCompiledTransform myXslTrans = new XslCompiledTransform();
                    string outputFilename;

                    //Header
                    outputFilename = tempOutputZipDirectory + @"\Vendor Invoice Header.xml";
                    myXslTrans.Load(xsltHeaderFileName);
                    XmlTextWriter myWriter = new XmlTextWriter(outputFilename, null);
                    myXslTrans.Transform(myXPathDoc, null, myWriter);

                    //Line
                    outputFilename = tempOutputZipDirectory + @"\Vendor Invoice Line.xml";
                    myXslTrans.Load(xsltLineFileName);
                    myWriter = new XmlTextWriter(outputFilename, null);
                    myXslTrans.Transform(myXPathDoc, null, myWriter);

                    //Attachment
                    outputFilename = tempOutputZipDirectory + @"\Vendor Invoice Document Attachment.xml"; 
                    myXslTrans.Load(xsltAttachmentFileName);
                    myWriter = new XmlTextWriter(outputFilename, null);
                    myXslTrans.Transform(myXPathDoc, null, myWriter);
                    myWriter.Dispose();

                    XmlDocument xmlDocument = new XmlDocument();
                    xmlDocument.Load(outputFilename);
                    XmlNode node = xmlDocument.SelectSingleNode("Document/VendorInvoiceDocumentAttachmentEntity/DOCUMENTID");
                    node.InnerText = Guid.NewGuid().ToString();
                    xmlDocument.Save(outputFilename);

                    //Copy manifest and package xml to folder
                    File.Copy(manifestXMLFileName, tempOutputZipDirectory + @"\" + Path.GetFileName(manifestXMLFileName), true);
                    File.Copy(packageXMLFileName, tempOutputZipDirectory + @"\" + Path.GetFileName(packageXMLFileName), true);

                    //Create ressource directory for image file
                    string ressourceDirectory = tempOutputZipDirectory + @"\Resources\Vendor invoice document attachment";
                    Directory.CreateDirectory(ressourceDirectory);
                    string resultZipFileName = string.Format("{0}\\{1}.{2}", tempOutputZipDirectory, fileName, "zip");

                    string imageFileName = string.Format("{0}\\{1}.{2}", Path.GetDirectoryName(dataMessage.FullPath), fileName, "tif");
                    //Copy image file to ressource folder
                    File.Copy(imageFileName, ressourceDirectory + @"\" + Path.GetFileName(imageFileName), true);

                    using (ZipFile zip = new ZipFile())
                    {
                        //zip.AddFiles(fileName2Zip, false, "");
                        zip.AddDirectory(tempOutputZipDirectory, "");
                        zip.Save(resultZipFileName);
                    }

                    FileStream resultZipFile = new FileStream(resultZipFileName, FileMode.Open);

                    string finalDestinationFileName = Path.GetDirectoryName(dataMessage.FullPath) + @"\" + Path.GetFileName(resultZipFileName);
                    FileStream finalDestinationFile = new FileStream(finalDestinationFileName, FileMode.OpenOrCreate);

                    resultZipFile.CopyTo(finalDestinationFile);
                    
                    finalDestinationFile.Close();
                    resultZipFile.Close();

                    string uploadSuccessXMLFullPath = Path.Combine(uploadSuccessFilePath, Path.GetFileName(dataMessage.FullPath));
                    string uploadSuccessImageFullPath = Path.Combine(uploadSuccessFilePath, Path.GetFileName(imageFileName));
                    File.Move(dataMessage.FullPath, uploadSuccessXMLFullPath);
                    File.Move(imageFileName, uploadSuccessImageFullPath);

                    dataMessage.FullPath = finalDestinationFileName;
                    dataMessage.Name = Path.GetFileName(resultZipFileName);              
                }
            }

            return dataMessage;
        }
    }
}
