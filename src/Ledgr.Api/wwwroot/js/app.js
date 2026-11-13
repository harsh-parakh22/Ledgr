'use strict';

const currency = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });
const dateTime = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });

const movementLabels = {
  StockIn: 'Stock in',
  OrderFulfill: 'Order',
  Adjustment: 'Correction',
  WriteOff: 'Write-off',
  OrderCancel: 'Cancellation'
};

const state = {
  products: { page: 1, totalPages: 0, items: [], totalCount: 0 },
  orders: { page: 1, totalPages: 0, items: [], totalCount: 0 },
  movements: { page: 1, totalPages: 0, totalCount: 0 },
  catalog: [],
  catalogSize: 0,
  productModal: null,
  adjustModal: null,
  orderModal: null
};

class ApiError extends Error {
  constructor(message, status) {
    super(message);
    this.status = status;
  }
}

async function api(path, { method = 'GET', body, headers = {} } = {}) {
  const init = { method, headers: { ...headers } };

  if (body !== undefined) {
    init.headers['Content-Type'] = 'application/json';
    init.body = JSON.stringify(body);
  }

  const response = await fetch(path, init);

  if (response.status === 204) {
    return null;
  }

  const payload = await response.json().catch(() => null);

  if (!response.ok) {
    throw new ApiError(describeProblem(payload, response.status), response.status);
  }

  return payload;
}

function describeProblem(payload, status) {
  if (payload && payload.errors) {
    const messages = Object.entries(payload.errors)
      .flatMap(([field, list]) => list.map((message) => `${field}: ${message}`));

    if (messages.length > 0) {
      return messages.slice(0, 2).join(' ');
    }
  }

  return (payload && (payload.detail || payload.title)) || `Request failed with status ${status}.`;
}

function esc(value) {
  return String(value === null || value === undefined ? '' : value).replace(/[&<>"']/g, (character) => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;'
  })[character]);
}

const money = (value) => currency.format(Number(value));
const when = (value) => dateTime.format(new Date(value));

function setText(id, value) {
  document.getElementById(id).textContent = value;
}

function value(id) {
  return document.getElementById(id).value.trim();
}

function emptyRow(columns, message) {
  return `<tr><td colspan="${columns}" class="empty">${esc(message)}</td></tr>`;
}

function toast(message, kind = 'ok') {
  const stack = document.getElementById('toasts');
  const item = document.createElement('div');
  item.className = kind === 'error' ? 'toast-item error' : 'toast-item';
  item.textContent = message;
  stack.appendChild(item);
  window.setTimeout(() => item.remove(), 4500);
}

function debounce(task, wait) {
  let timer = null;
  return (...args) => {
    window.clearTimeout(timer);
    timer = window.setTimeout(() => task(...args), wait);
  };
}

async function guard(task) {
  try {
    await task();
  } catch (error) {
    toast(error.message, 'error');
  }
}

/* Data loading */

async function loadStats() {
  const kpis = await api('/api/dashboard/kpis');

  setText('stat-products', kpis.activeProducts);
  setText('stat-low-stock', kpis.lowStockCount);
  setText('stat-open-orders', kpis.openOrders);
  setText('stat-value', money(kpis.inventoryValue));
}

async function loadCategories() {
  const categories = await api('/api/products/categories');
  const filter = document.getElementById('product-category');
  const current = filter.value;

  filter.innerHTML = '<option value="">All categories</option>' +
    categories.map((category) => `<option value="${esc(category)}">${esc(category)}</option>`).join('');

  filter.value = categories.includes(current) ? current : '';

  document.getElementById('category-options').innerHTML =
    categories.map((category) => `<option value="${esc(category)}"></option>`).join('');
}

async function loadCatalog() {
  const result = await api('/api/products?page=1&pageSize=100');

  state.catalog = result.items;
  state.catalogSize = result.totalCount;
}

async function loadProducts(page = 1) {
  const params = new URLSearchParams({ page: String(page), pageSize: '10' });
  const search = value('product-search');
  const category = value('product-category');

  if (search) {
    params.set('search', search);
  }

  if (category) {
    params.set('category', category);
  }

  if (document.getElementById('product-low-stock').checked) {
    params.set('onlyLowStock', 'true');
  }

  const result = await api(`/api/products?${params}`);
  state.products = result;

  renderProducts(result);
  renderPager('product-pager', result, 'products');
  setText('product-summary', `${result.totalCount} product${result.totalCount === 1 ? '' : 's'}`);
}

function renderProducts(result) {
  const rows = document.getElementById('product-rows');

  if (result.items.length === 0) {
    rows.innerHTML = emptyRow(8, 'No products match this filter.');
    return;
  }

  rows.innerHTML = result.items.map((product) => `
    <tr>
      <td><span class="sku">${esc(product.sku)}</span></td>
      <td>
        <span class="product-name">${esc(product.name)}</span>
        ${product.description ? `<span class="product-note">${esc(product.description)}</span>` : ''}
      </td>
      <td>${esc(product.category)}</td>
      <td class="num">${money(product.unitPrice)}</td>
      <td class="num ${product.isLowStock ? 'low' : ''}">${product.quantityOnHand}</td>
      <td class="num muted">${product.reorderLevel}</td>
      <td>${product.isLowStock
        ? '<span class="pill pill-low">Low</span>'
        : '<span class="pill pill-ok">In stock</span>'}</td>
      <td class="actions">
        <button class="link-btn" type="button" data-action="adjust" data-id="${product.id}">Adjust</button>
        <button class="link-btn" type="button" data-action="edit" data-id="${product.id}">Edit</button>
        <button class="link-btn danger" type="button" data-action="deactivate" data-id="${product.id}">Deactivate</button>
      </td>
    </tr>`).join('');
}

async function loadOrders(page = 1) {
  const result = await api(`/api/orders?page=${page}&pageSize=10`);
  state.orders = result;

  const rows = document.getElementById('order-rows');

  if (result.items.length === 0) {
    rows.innerHTML = emptyRow(7, 'No orders yet.');
  } else {
    rows.innerHTML = result.items.map((order) => `
      <tr>
        <td><span class="sku">${esc(order.orderNumber)}</span></td>
        <td>
          <span class="product-name">${esc(order.customerName)}</span>
          ${order.customerEmail ? `<span class="product-note">${esc(order.customerEmail)}</span>` : ''}
        </td>
        <td class="num muted">${order.items.length}</td>
        <td class="num">${money(order.totalAmount)}</td>
        <td>${order.status === 'Cancelled'
          ? '<span class="pill pill-muted">Cancelled</span>'
          : '<span class="pill pill-ok">Confirmed</span>'}</td>
        <td class="muted">${when(order.createdAtUtc)}</td>
        <td class="actions">${order.status === 'Confirmed'
          ? `<button class="link-btn danger" type="button" data-action="cancel-order" data-id="${order.id}">Cancel</button>`
          : ''}</td>
      </tr>`).join('');
  }

  renderPager('order-pager', result, 'orders');
  setText('order-summary', `${result.totalCount} order${result.totalCount === 1 ? '' : 's'}`);
}

async function loadMovements(page = 1) {
  const result = await api(`/api/inventory/movements?page=${page}&pageSize=15`);
  state.movements = result;

  const rows = document.getElementById('movement-rows');

  if (result.items.length === 0) {
    rows.innerHTML = emptyRow(6, 'No stock movements recorded yet.');
  } else {
    rows.innerHTML = result.items.map((movement) => {
      const positive = movement.quantityChange > 0;

      return `
        <tr>
          <td class="muted">${when(movement.createdAtUtc)}</td>
          <td>
            <span class="product-name">${esc(movement.productName)}</span>
            <span class="product-note"><span class="sku">${esc(movement.sku)}</span></span>
          </td>
          <td>${esc(movementLabels[movement.type] || movement.type)}</td>
          <td class="num ${positive ? '' : 'low'}">${positive ? '+' : ''}${movement.quantityChange}</td>
          <td class="num muted">${movement.previousQuantity} &rarr; ${movement.newQuantity}</td>
          <td>
            ${esc(movement.reason)}
            ${movement.referenceId ? `<span class="product-note"><span class="sku">${esc(movement.referenceId)}</span></span>` : ''}
          </td>
        </tr>`;
    }).join('');
  }

  renderPager('movement-pager', result, 'movements');
  setText('movement-summary', `${result.totalCount} movement${result.totalCount === 1 ? '' : 's'}`);
}

function renderPager(id, result, resource) {
  const pager = document.getElementById(id);

  if (result.totalPages <= 1) {
    pager.innerHTML = '';
    return;
  }

  pager.innerHTML = `
    <button class="pager-btn" type="button" data-action="page" data-resource="${resource}" data-page="${result.page - 1}" ${result.page <= 1 ? 'disabled' : ''}>Previous</button>
    <span class="pager-label">Page ${result.page} of ${result.totalPages}</span>
    <button class="pager-btn" type="button" data-action="page" data-resource="${resource}" data-page="${result.page + 1}" ${result.page >= result.totalPages ? 'disabled' : ''}>Next</button>`;
}

/* Products */

function findProduct(id) {
  return state.products.items.find((product) => product.id === Number(id));
}

function openProductModal(product) {
  const form = document.getElementById('product-form');
  form.reset();
  form.classList.remove('was-validated');

  const isEdit = Boolean(product);

  document.getElementById('product-id').value = isEdit ? product.id : '';
  document.getElementById('product-modal-title').textContent = isEdit ? 'Edit product' : 'New product';
  document.getElementById('product-submit').textContent = isEdit ? 'Save changes' : 'Create product';
  document.getElementById('product-sku').disabled = isEdit;
  document.getElementById('product-sku').value = isEdit ? product.sku : '';
  document.getElementById('product-initial-wrap').classList.toggle('d-none', isEdit);
  document.getElementById('product-active-wrap').classList.toggle('d-none', !isEdit);

  if (isEdit) {
    document.getElementById('product-name').value = product.name;
    document.getElementById('product-description').value = product.description || '';
    document.getElementById('product-category-input').value = product.category;
    document.getElementById('product-price').value = product.unitPrice;
    document.getElementById('product-reorder').value = product.reorderLevel;
    document.getElementById('product-active').checked = product.isActive;
  } else {
    document.getElementById('product-initial').value = '0';
    document.getElementById('product-reorder').value = '5';
  }

  state.productModal.show();
}

async function saveProduct(event) {
  event.preventDefault();

  const form = event.currentTarget;

  if (!form.checkValidity()) {
    form.classList.add('was-validated');
    return;
  }

  const id = value('product-id');
  const payload = {
    name: value('product-name'),
    description: value('product-description') || null,
    category: value('product-category-input'),
    unitPrice: Number(value('product-price')),
    reorderLevel: Number(value('product-reorder')),
    isActive: document.getElementById('product-active').checked
  };

  const submit = document.getElementById('product-submit');
  submit.disabled = true;

  try {
    if (id) {
      await api(`/api/products/${id}`, { method: 'PUT', body: payload });
      toast('Product updated.');
    } else {
      await api('/api/products', {
        method: 'POST',
        body: {
          ...payload,
          sku: value('product-sku'),
          initialQuantity: Number(value('product-initial')) || 0
        }
      });
      toast('Product created.');
    }

    state.productModal.hide();
    await Promise.all([loadProducts(state.products.page), loadCategories(), loadStats(), loadCatalog()]);
  } catch (error) {
    toast(error.message, 'error');
  } finally {
    submit.disabled = false;
  }
}

async function deactivateProduct(id) {
  const product = findProduct(id);

  if (!product) {
    return;
  }

  if (!window.confirm(`Deactivate ${product.sku}? It stays in past orders and movements.`)) {
    return;
  }

  try {
    await api(`/api/products/${id}`, { method: 'DELETE' });
    toast('Product deactivated.');
    await Promise.all([loadProducts(state.products.page), loadStats(), loadCatalog()]);
  } catch (error) {
    toast(error.message, 'error');
  }
}

/* Stock adjustments */

function openAdjustModal(product) {
  const form = document.getElementById('adjust-form');
  form.reset();
  form.classList.remove('was-validated');

  document.getElementById('adjust-product-id').value = product.id;
  document.getElementById('adjust-target').textContent =
    `${product.sku} — ${product.name} · ${product.quantityOnHand} on hand`;
  document.getElementById('adjust-type').value = '1';

  state.adjustModal.show();
}

async function saveAdjustment(event) {
  event.preventDefault();

  const form = event.currentTarget;

  if (!form.checkValidity()) {
    form.classList.add('was-validated');
    return;
  }

  const quantity = Number(value('adjust-quantity'));

  if (!Number.isInteger(quantity) || quantity === 0) {
    toast('Enter a non-zero whole number.', 'error');
    return;
  }

  const submit = document.getElementById('adjust-submit');
  submit.disabled = true;

  try {
    await api('/api/inventory/adjust', {
      method: 'POST',
      body: {
        productId: Number(value('adjust-product-id')),
        quantityChange: quantity,
        type: Number(value('adjust-type')),
        reason: value('adjust-reason'),
        referenceId: value('adjust-reference') || null
      }
    });

    state.adjustModal.hide();
    toast('Movement recorded.');
    await Promise.all([loadProducts(state.products.page), loadStats(), loadCatalog()]);

    if (document.getElementById('pane-movements').classList.contains('active')) {
      await loadMovements(1);
    }
  } catch (error) {
    toast(error.message, 'error');
  } finally {
    submit.disabled = false;
  }
}

/* Orders */

function newIdempotencyKey() {
  if (window.crypto && typeof window.crypto.randomUUID === 'function') {
    return window.crypto.randomUUID();
  }

  return `key-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function openOrderModal() {
  if (state.catalog.length === 0 && state.catalogSize === 0) {
    toast('Add a product before placing an order.', 'error');
    return;
  }

  const form = document.getElementById('order-form');
  form.reset();
  form.classList.remove('was-validated');

  document.getElementById('order-lines').innerHTML = '';
  state.orderKey = newIdempotencyKey();

  addOrderLine();
  updateOrderTotal();
  state.orderModal.show();
}

function addOrderLine() {
  const options = state.catalog
    .map((product) => `<option value="${product.id}">${esc(product.sku)} — ${esc(product.name)} (${product.quantityOnHand} on hand)</option>`)
    .join('');

  const truncated = state.catalogSize > state.catalog.length
    ? `<option value="" disabled>Only the first ${state.catalog.length} products are listed</option>`
    : '';

  const line = document.createElement('div');
  line.className = 'order-line';
  line.innerHTML = `
    <select class="form-select form-select-sm line-product" aria-label="Product">${options}${truncated}</select>
    <input class="form-control form-control-sm line-quantity" type="number" min="1" step="1" value="1" aria-label="Quantity">
    <button class="link-btn danger" type="button" data-action="remove-line">Remove</button>`;

  document.getElementById('order-lines').appendChild(line);
}

function updateOrderTotal() {
  const total = Array.from(document.querySelectorAll('.order-line')).reduce((sum, line) => {
    const productId = Number(line.querySelector('.line-product').value);
    const quantity = Number(line.querySelector('.line-quantity').value);
    const product = state.catalog.find((item) => item.id === productId);

    if (!product || !quantity || quantity < 1) {
      return sum;
    }

    return sum + (Number(product.unitPrice) * quantity);
  }, 0);

  document.getElementById('order-total').textContent = money(total);
}

async function submitOrder(event) {
  event.preventDefault();

  const form = event.currentTarget;

  if (!form.checkValidity()) {
    form.classList.add('was-validated');
    return;
  }

  const items = Array.from(document.querySelectorAll('.order-line'))
    .map((line) => ({
      productId: Number(line.querySelector('.line-product').value),
      quantity: Number(line.querySelector('.line-quantity').value)
    }))
    .filter((item) => item.productId > 0 && item.quantity > 0);

  if (items.length === 0) {
    toast('Add at least one line.', 'error');
    return;
  }

  const submit = document.getElementById('order-submit');
  submit.disabled = true;

  try {
    const order = await api('/api/orders', {
      method: 'POST',
      headers: { 'Idempotency-Key': state.orderKey },
      body: {
        customerName: value('order-customer'),
        customerEmail: value('order-email') || null,
        items
      }
    });

    state.orderModal.hide();
    toast(`Order ${order.orderNumber} placed.`);

    await Promise.all([loadOrders(1), loadProducts(state.products.page), loadStats(), loadCatalog()]);
  } catch (error) {
    toast(error.message, 'error');
  } finally {
    submit.disabled = false;
  }
}

async function cancelOrder(id) {
  if (!window.confirm('Cancel this order and return its units to stock?')) {
    return;
  }

  try {
    await api(`/api/orders/${id}/cancel`, { method: 'POST' });
    toast('Order cancelled and stock returned.');
    await Promise.all([loadOrders(state.orders.page), loadProducts(state.products.page), loadStats(), loadCatalog()]);
  } catch (error) {
    toast(error.message, 'error');
  }
}

/* Wiring */

function handleClick(event) {
  const trigger = event.target.closest('[data-action]');

  if (!trigger) {
    return;
  }

  const { action, id, page, resource } = trigger.dataset;

  switch (action) {
    case 'new-product':
      openProductModal(null);
      break;
    case 'edit': {
      const product = findProduct(id);
      if (product) {
        openProductModal(product);
      }
      break;
    }
    case 'adjust': {
      const product = findProduct(id);
      if (product) {
        openAdjustModal(product);
      }
      break;
    }
    case 'deactivate':
      guard(() => deactivateProduct(id));
      break;
    case 'new-order':
      openOrderModal();
      break;
    case 'add-line':
      addOrderLine();
      updateOrderTotal();
      break;
    case 'remove-line':
      trigger.closest('.order-line').remove();
      updateOrderTotal();
      break;
    case 'cancel-order':
      guard(() => cancelOrder(id));
      break;
    case 'reload-movements':
      guard(() => loadMovements(1));
      break;
    case 'page':
      guard(() => {
        if (resource === 'products') {
          return loadProducts(Number(page));
        }
        if (resource === 'orders') {
          return loadOrders(Number(page));
        }
        return loadMovements(Number(page));
      });
      break;
  }
}

function onTabShown(event) {
  const target = event.target.getAttribute('data-bs-target');

  if (target === '#pane-orders') {
    guard(() => loadOrders(state.orders.page));
  } else if (target === '#pane-movements') {
    guard(() => loadMovements(state.movements.page));
  }
}

async function refreshAll() {
  await Promise.all([loadStats(), loadCategories(), loadProducts(1), loadCatalog()]);
}

document.addEventListener('DOMContentLoaded', () => {
  state.productModal = new bootstrap.Modal(document.getElementById('product-modal'));
  state.adjustModal = new bootstrap.Modal(document.getElementById('adjust-modal'));
  state.orderModal = new bootstrap.Modal(document.getElementById('order-modal'));

  document.getElementById('product-form').addEventListener('submit', saveProduct);
  document.getElementById('adjust-form').addEventListener('submit', saveAdjustment);
  document.getElementById('order-form').addEventListener('submit', submitOrder);

  document.getElementById('order-lines').addEventListener('input', updateOrderTotal);
  document.getElementById('order-lines').addEventListener('change', updateOrderTotal);

  document.getElementById('product-search').addEventListener('input', debounce(() => guard(() => loadProducts(1)), 250));
  document.getElementById('product-category').addEventListener('change', () => guard(() => loadProducts(1)));
  document.getElementById('product-low-stock').addEventListener('change', () => guard(() => loadProducts(1)));

  document.addEventListener('click', handleClick);
  document.querySelectorAll('[data-bs-toggle="tab"]').forEach((tab) => tab.addEventListener('shown.bs.tab', onTabShown));

  guard(refreshAll);
});
