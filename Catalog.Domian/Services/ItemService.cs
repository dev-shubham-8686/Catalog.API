using Catalog.Domain.Entities;
using Catalog.Domain.Logging;
using Catalog.Domain.Repositories;
using Catalog.Domain.Requests.Item;
using Catalog.Domain.Responses.Item;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Catalog.Domain.Services
{
    public class ItemService : IItemService
    {
        private readonly IItemRepository _itemRepository;
        private readonly ILogger<IItemService> _logger;

        public ItemService(IItemRepository itemRepository, ILogger<IItemService> logger)
        {
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
            _logger = logger;
        }
        public async Task<AddItemResponse> AddItemAsync(AddItemRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var item = new Item
            {
                Name = request.Name,
                Description = request.Description,
            };

            var result = await _itemRepository.AddAsync(item, cancellationToken);
            var modifiedRecords = await _itemRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(Logging.Events.Add, Messages.NumberOfRecordAffected_modifiedRecords, modifiedRecords);
            _logger.LogInformation(Logging.Events.Add, Messages.ChangesApplied_id, result.Id);

            return new AddItemResponse
            {
                Id = result.Id,
                Name = result.Name,
                Description = result.Description
            };


        }

        public async Task DeleteItemAsync(DeleteItemRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var existingRecord = await _itemRepository.GetItemAsync(request.Id, cancellationToken);

            int modifiedRecords = 0;

            if (existingRecord != null)
            {
                await _itemRepository.DeleteAsync(request.Id, cancellationToken);

                modifiedRecords = await _itemRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(Logging.Events.Delete, Messages.NumberOfRecordAffected_modifiedRecords,
                modifiedRecords);

        }

        public async Task<EditItemResponse> EditItemAsync(EditItemRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var existingRecord = await _itemRepository.GetAsync(request.Id, cancellationToken);

            if (existingRecord == null) throw new ArgumentException($"Entity with {request.Id} is not present");

            existingRecord.Name = request.Name;
            existingRecord.Description = request.Description;

            var result = await _itemRepository.UpdateAsync(existingRecord, cancellationToken);

            var modifiedRecords = await _itemRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(Logging.Events.Edit, Messages.NumberOfRecordAffected_modifiedRecords,
                modifiedRecords);

            _logger.LogInformation(Logging.Events.Edit, Messages.ChangesApplied_id, result.Id);

            return new EditItemResponse
            {
                Id = result.Id,
                Name = result.Name,
                Description = result.Description
            };
        }

        public async Task<GetItemResponse> GetItemAsync(GetItemRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var entity = await _itemRepository.GetAsync(request.Id, cancellationToken);

            _logger.LogInformation(Logging.Events.GetById, Messages.TargetEntityChanged_id, entity?.Id);

            if (entity == null) throw new ArgumentException($"Entity with {request.Id} is not present");

            var response = new GetItemResponse
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                Genre = entity.Genre != null ? new GetItemResponse.GenreInfo
                {
                    GenreId = entity.Genre.GenreId,
                    GenreDescription = entity.Genre.GenreDescription
                } : null,
                Artist = entity.Artist != null ? new GetItemResponse.ArtistInfo
                {
                    ArtistId = entity.Artist.ArtistId,
                    ArtistName = entity.Artist.ArtistName
                } : null

            };

            return response;
        }

        public async Task<GetItemsResponse> GetItemsAsync(CancellationToken cancellationToken = default)
        {
            var result = await _itemRepository.GetAsync(cancellationToken);

            _logger.LogInformation(Logging.Events.GetById, Messages.NumberOfRecordAffected_modifiedRecords, result.Count());

            var response = new GetItemsResponse();

            response.Items = result.Select(entity => new GetItemResponse
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                Genre = entity.Genre != null ? new GetItemResponse.GenreInfo
                {
                    GenreId = entity.Genre.GenreId,
                    GenreDescription = entity.Genre.GenreDescription
                } : null,
                Artist = entity.Artist != null ? new GetItemResponse.ArtistInfo
                {
                    ArtistId = entity.Artist.ArtistId,
                    ArtistName = entity.Artist.ArtistName
                } : null
            }).ToList();

            return response;
        }
    }
}
