﻿using Microsoft.AspNetCore.Mvc;
using ScreenSound.API.Requests;
using ScreenSound.API.Response;
using ScreenSound.Banco;
using ScreenSound.Modelos;
using ScreenSound.Shared.Dados.Modelos;
using System.Security.Claims;

namespace ScreenSound.API.Endpoints;

public static class ArtistasExtensions
{
    public static void AddEndPointsArtistas(this WebApplication app)
    {
        var group = app.MapGroup("artistas")
            .RequireAuthorization()
            .WithTags("Artistas");

        #region Endpoint Artistas
        group.MapGet("/", ([FromServices] DAL<Artista> dal) =>
        {
            var listaDeArtistas = dal.Listar();
            if (listaDeArtistas is null)
            {
                return Results.NotFound();
            }
            var listaDeArtistaResponse = EntityListToResponseList(listaDeArtistas);
            return Results.Ok(listaDeArtistaResponse);
        });

        group.MapGet("/{nome}", ([FromServices] DAL<Artista> dal, string nome) =>
        {
            var artista = dal.RecuperarPor(a => a.Nome.ToUpper().Equals(nome.ToUpper()));
            if (artista is null)
            {
                return Results.NotFound();
            }
            return Results.Ok(EntityToResponse(artista));

        });

        group.MapPost("/", async ([FromServices]IHostEnvironment env,[FromServices] DAL<Artista> dal, [FromBody] ArtistaRequest artistaRequest) =>
        {
            
            var nome = artistaRequest.nome.Trim();
            var imagemArtista = DateTime.Now.ToString("ddMMyyyyhhss") + "." + nome + ".jpg";

            var path = Path.Combine(env.ContentRootPath,
                "wwwroot", "FotosPerfil", imagemArtista);

            using MemoryStream ms = new MemoryStream(Convert.FromBase64String(artistaRequest.fotoPerfil!));
            using FileStream fs = new(path, FileMode.Create);
            await ms.CopyToAsync(fs);

            var artista = new Artista(artistaRequest.nome, artistaRequest.bio) { FotoPerfil = $"/FotosPerfil/{imagemArtista}" };

            dal.Adicionar(artista);
            return Results.Ok();
        });

        group.MapDelete("/{id}", ([FromServices] DAL<Artista> dal, int id) => {
            var artista = dal.RecuperarPor(a => a.Id == id);
            if (artista is null)
            {
                return Results.NotFound();
            }
            dal.Deletar(artista);
            return Results.NoContent();

        });

        group.MapPut("/", ([FromServices] DAL<Artista> dal, [FromBody] ArtistaRequestEdit artistaRequestEdit) => {
            var artistaAAtualizar = dal.RecuperarPor(a => a.Id == artistaRequestEdit.Id);
            if (artistaAAtualizar is null)
            {
                return Results.NotFound();
            }
            artistaAAtualizar.Nome = artistaRequestEdit.nome;
            artistaAAtualizar.Bio = artistaRequestEdit.bio;        
            dal.Atualizar(artistaAAtualizar);
            return Results.Ok();
        });

        group.MapPost("/avaliacao", (
            HttpContext context,
            [FromBody] AvaliacaoArtistaRequest request,
            [FromServices] DAL<Artista> dalArtista,
            [FromServices] DAL<PessoaComAcesso> dalPessoa
            ) =>
        {
            var artista = dalArtista.RecuperarPor(a => a.Id == request.ArtistaId);
            if (artista is null) return Results.NotFound();

            var email = context.User.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
                ?? throw new InvalidOperationException("Pessoa não está conectada.");

            var pessoa = dalPessoa.RecuperarPor(p => p.Email == email);

            if (pessoa is null) throw new InvalidOperationException("Pessoa não está conectada");

            var avaliacao = artista.Avaliacoes
                .FirstOrDefault(a => a.ArtistaId == artista.Id && a.PessoaId == pessoa.Id);

            if (avaliacao is null)
                artista.AdicionarNota(pessoa.Id, request.Nota);
            else
                avaliacao.Nota = request.Nota;


            dalArtista.Atualizar(artista);

            return Results.Created();
        });

        group.MapGet("/{Id}/avaliacao", (
            int Id,
            HttpContext context,
            [FromServices] DAL<Artista> artistaDal,
            [FromServices] DAL<PessoaComAcesso> pessoaDal) =>
        {
            var email = context.User.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value
                ?? throw new InvalidOperationException("Pessoa não está conectada.");

            var pessoa = pessoaDal.RecuperarPor(p => p.Email == email);

            if (pessoa is null) throw new InvalidOperationException("Pessoa não está conectada");

            var artista = artistaDal.RecuperarPor(a => a.Id == Id);
            if (artista is null) return Results.NotFound("Artista não encontrado.");

            var avaliacao = artista.Avaliacoes.FirstOrDefault(a => a.ArtistaId == Id && a.PessoaId == pessoa.Id);
            if (avaliacao is null) return Results.NotFound("Nenhuma avaliação cadastrada para esse artista.");

            return Results.Ok(new AvaliacaoArtistaRequest(Id, avaliacao.Nota));
        });
        #endregion
    }

    private static ICollection<ArtistaResponse> EntityListToResponseList(IEnumerable<Artista> listaDeArtistas)
    {
        return listaDeArtistas.Select(a => EntityToResponse(a)).ToList();
    }

    private static ArtistaResponse EntityToResponse(Artista artista)
    {
        return new ArtistaResponse(artista.Id, artista.Nome, artista.Bio, artista.FotoPerfil)
        {
            Classificacao = artista
                .Avaliacoes
                .Select(a => a.Nota)
                .DefaultIfEmpty(0)
                .Average()
        };
    }

  
}
