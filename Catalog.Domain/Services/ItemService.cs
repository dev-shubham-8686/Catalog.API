using Catalog.Domain.Entities;
using Catalog.Domain.Logging;
using Catalog.Domain.Mappers;
using Catalog.Domain.Repositories;
using Catalog.Domain.Requests.Item;
using Catalog.Domain.Responses;
using Catalog.Domain.Responses.Item;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Catalog.Domain.Services
{
    public class ItemService : IItemService
    {
        private readonly IItemRepository _itemRepository;
        private readonly ILogger<ItemService> _logger;
        private readonly IValidator<AddItemRequest> _addItemRequestValidator;
        public ItemService(IItemRepository itemRepository, ILogger<ItemService> logger, IValidator<AddItemRequest> addItemRequestValidator)
        {
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
            _logger = logger;
            _addItemRequestValidator = addItemRequestValidator;
        }
        public async Task<AddItemResponse> AddItemAsync(AddItemRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            await _addItemRequestValidator.ValidateAndThrowAsync(request, cancellationToken);

            var item = request.MapToItem();

            var result = await _itemRepository.AddAsync(item, cancellationToken);
            var modifiedRecords = await _itemRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(Logging.Events.Add, Messages.NumberOfRecordAffected_modifiedRecords, modifiedRecords);
            _logger.LogInformation(Logging.Events.Add, Messages.ChangesApplied_id, result.Id);
            
            return result.MapToAddItemResponse();


        }
        public async Task DeleteItemAsync(DeleteItemRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var existingRecord = await _itemRepository.FindItemAsync(request.Id, cancellationToken);

            int modifiedRecords = 0;

            if (existingRecord != null)
            {
                await _itemRepository.DeleteAsync(request.Id, cancellationToken);

                modifiedRecords = await _itemRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(Logging.Events.Delete, Messages.NumberOfRecordAffected_modifiedRecords,
                modifiedRecords);

        }
        public async Task<EditItemResponse> EditItemAsync(Guid id,EditItemRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var existingRecord = await _itemRepository.FindItemAsync(id, cancellationToken);

            if (existingRecord == null) throw new ArgumentException($"Entity with {id} is not present");

            existingRecord.Name = request.Name;
            existingRecord.Description = request.Description;

            var result =  _itemRepository.Update(existingRecord, cancellationToken);

            var modifiedRecords = await _itemRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(Logging.Events.Edit, Messages.NumberOfRecordAffected_modifiedRecords,
                modifiedRecords);

            _logger.LogInformation(Logging.Events.Edit, Messages.ChangesApplied_id, result.Id);

            return result.MapToEditItemResponse(id);
        }
        public async Task<GetItemResponse> GetItemAsync(GetItemRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var result = await _itemRepository.GetAsync(request.Id, cancellationToken);

            _logger.LogInformation(Logging.Events.GetById, Messages.TargetEntityChanged_id, result?.Id);

            if (result == null) throw new ArgumentException($"Entity with {request.Id} is not present");

            return result.MapToGetItemResponse();
        }
        public async Task<GetItemsResponse> GetItemsAsync(CancellationToken cancellationToken = default)
        {
            var result = await _itemRepository.GetAsync(cancellationToken);

            _logger.LogInformation(Logging.Events.Get, Messages.NumberOfRecordAffected_modifiedRecords, result.Count());

            return result.MapToGetItemsResponse();
        }
        public async Task<PaginatedItemResponseModel<GetItemResponse>> GetItemsAsync(int pageSize, int pageIndex, CancellationToken cancellationToken = default)
        {
           var result = await _itemRepository.GetAsync(pageSize, pageIndex, cancellationToken);

           var totalItems = await _itemRepository.CountAsync(cancellationToken);
           _logger.LogInformation(Logging.Events.Get, Messages.NumberOfRecordAffected_modifiedRecords, totalItems);

            return result.MapToPaginatedItemResponseModel(pageSize, pageIndex, totalItems);
        }

    }
}
