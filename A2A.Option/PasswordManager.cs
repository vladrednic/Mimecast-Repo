using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Option {
    public class PasswordManager {
        public bool ResetPassword { get; set; }
        private string GetEncryptedPasswordPasswordFromFile(string fileName = null) {
            if (string.IsNullOrEmpty(fileName))
                fileName = GetPasswordFileName();
            if (!File.Exists(fileName))
                return null;
            var encryptedPass = string.Empty;
            using (var sr = File.OpenText(fileName)) {
                encryptedPass = sr.ReadLine();
            }
            return encryptedPass;
        }

        public void DeletePasswordFile() {
            var fileName = GetPasswordFileName();
            if (File.Exists(fileName))
                File.Delete(fileName);
        }

        public bool InputFromUser { get; private set; }

        public string GetPasswordPlainText() {
            var encryptedPass = string.Empty;
            var plainTextPassword = string.Empty;
            encryptedPass = GetEncryptedPasswordPasswordFromFile();
            if (ResetPassword || string.IsNullOrEmpty(encryptedPass)) {
                plainTextPassword = ReadPasswordFromConsole();
                encryptedPass = SymmetricEncryption.Encrypt(plainTextPassword);
                SavePassword(encryptedPass);
                InputFromUser = true;
            }
            else {
                plainTextPassword = SymmetricEncryption.Decrypt(encryptedPass);
                InputFromUser = false;
            }
            return plainTextPassword;
        }

        private string GetPasswordFileName() {
            var folder = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var fileName = Path.Combine(folder, "admin.pwd");
            return fileName;
        }

        private void SavePassword(string encryptedPass) {
            var fileName = GetPasswordFileName();
            using (var sw = File.CreateText(fileName)) {
                sw.WriteLine(encryptedPass);
            }
        }

        public string ReadPasswordFromConsole() {
            Console.Write("Input new admin account password: ");
            var sb = new StringBuilder();
            do {
                var c = Console.ReadKey(true);
                if (c.Key == ConsoleKey.Enter)
                    break;
                sb.Append(c.KeyChar);
                Console.Write('*');
            } while (true);
            Console.WriteLine();
            return sb.ToString();
        }
    }
}
