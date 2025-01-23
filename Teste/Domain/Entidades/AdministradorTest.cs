using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using minimal_api.Dominio.Entidades;

namespace Teste.Domain.Entidades
{
    [TestClass]
    public class AdministradorTeste
    {
        [TestMethod]
        public void TestarGetSetPropriedade(){
            var adm = new Administrador();
        
            adm.Id = 1;
            adm.Email = "email@gmail.com";
            adm.Senha = "teste";
            adm.Perfil = "Adm";

            Assert.AreEqual(1,adm.Id);
            Assert.AreEqual("email@gmail.com",adm.Email);
            Assert.AreEqual("teste",adm.Senha);
            Assert.AreEqual("Adm",adm.Perfil);
        }
    }
}