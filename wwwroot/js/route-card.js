document.addEventListener('DOMContentLoaded', () => {
  const pages = Array.from(document.querySelectorAll('.sheet'));
  let currentPage = 0;

  function showPage(index) {
    currentPage = Math.max(0, Math.min(index, pages.length - 1));
    pages.forEach((page, pageIndex) => {
      const active = pageIndex === currentPage;
      page.classList.toggle('active', active);
      page.setAttribute('aria-hidden', String(!active));
    });
    const heading = pages[currentPage].querySelector('h2');
    if (heading) {
      heading.setAttribute('tabindex', '-1');
      heading.focus({ preventScroll: true });
    }
  }

  // Show all pages by default (single page view)
  pages.forEach((page) => {
    page.classList.add('active');
    page.setAttribute('aria-hidden', 'false');
  });

  // Navigation disabled for single page view
  document.querySelectorAll('[data-page="next"]').forEach(button => {
    button.style.display = 'none';
  });
  document.querySelectorAll('[data-page="previous"]').forEach(button => {
    button.style.display = 'none';
  });

  function nextRowNumber(tbody, pattern) {
    return Array.from(tbody.querySelectorAll('input[name]')).reduce((highest, input) => {
      const match = input.name.match(pattern);
      return match ? Math.max(highest, Number(match[1])) : highest;
    }, 0) + 1;
  }

  function addInputCell(row, name, label, type = 'text', value = '') {
    const cell = document.createElement('td');
    const isDescription = name.startsWith('operationDescription');
    const input = isDescription ? document.createElement('textarea') : document.createElement('input');
    input.name = name;
    input.value = value ?? '';
    input.setAttribute('aria-label', label);
    if (!isDescription) {
      input.type = type;
      if (type === 'number') input.min = '0';
    }
    cell.append(input);
    row.append(cell);
  }

  function addDeleteButton(row, label) {
    const cell = document.createElement('td');
    cell.className = 'delete-cell';
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'row-delete-button';
    button.textContent = 'Delete';
    button.setAttribute('aria-label', label);
    button.addEventListener('click', () => {
      const table = row.closest('table');
      row.remove();
      if (table.classList.contains('trace-table')) renumberTraceRows(table);
    });
    cell.append(button);
    row.append(cell);
  }

  function renumberTraceRows(table) {
    table.querySelectorAll('tbody tr').forEach((row, index) => {
      row.cells[0].textContent = String(index + 1);
    });
  }

  function addActionColumn(table) {
    const heading = document.createElement('th');
    heading.className = 'actions-heading';
    heading.setAttribute('aria-label', 'Row actions');
    table.tHead.rows[0].append(heading);
    table.tBodies[0].querySelectorAll('tr').forEach((row, index) => {
      addDeleteButton(row, `Delete row ${index + 1}`);
    });
  }

  const traceTable = document.querySelector('.trace-table');
  const operationsTable = document.querySelector('.operations-table');
  if (traceTable) addActionColumn(traceTable);
  if (operationsTable) addActionColumn(operationsTable);

  document.querySelectorAll('[data-add-row]').forEach(button => {
    button.addEventListener('click', () => {
      const isTrace = button.dataset.addRow === 'traceability';
      const table = isTrace ? traceTable : operationsTable;
      if (!table) return;
      const tbody = table.tBodies[0];
      const row = document.createElement('tr');
      const displayNumber = tbody.rows.length + 1;

      if (isTrace) {
        const rowNumber = nextRowNumber(tbody, /^billetId(\d+)$/);
        const serial = document.createElement('td');
        serial.textContent = String(displayNumber);
        row.append(serial);
        addInputCell(row, `billetId${rowNumber}`, `Serial ${displayNumber} billet identification`);
        addInputCell(row, `billetRemarks${rowNumber}`, `Serial ${displayNumber} remarks`);
      } else {
        const rowNumber = nextRowNumber(tbody, /^operationNo(\d+)$/);
        const fields = [
          ['operationNo', 'operation number', 'text'],
          ['operationDescription', 'operation description', 'text'],
          ['operationDepWcr', 'department or WCR', 'text'],
          ['operationAccQty', 'accepted quantity', 'number'],
          ['operationRejQty', 'rejected quantity', 'number'],
          ['operationStamp', 'stamp', 'text'],
          ['operationSign', 'signature', 'text']
        ];
        fields.forEach(([name, label, type]) => {
          addInputCell(row, `${name}${rowNumber}`, `Operation ${displayNumber} ${label}`, type);
        });
      }

      addDeleteButton(row, `Delete row ${displayNumber}`);
      tbody.append(row);
    });
  });

  function setField(name, value) {
    const field = document.querySelector(`[name="${name}"]`);
    if (field && value !== null && value !== undefined) field.value = value;
  }

  function populateOperations(operations) {
    if (!operationsTable || !Array.isArray(operations) || operations.length === 0) return;
    const tbody = operationsTable.tBodies[0];
    tbody.innerHTML = '';
    operations.forEach((operation, index) => {
      const row = document.createElement('tr');
      const rowNumber = index + 1;
      addInputCell(row, `operationNo${rowNumber}`, `Operation ${rowNumber} number`, 'text', operation.opNo);
      addInputCell(row, `operationDescription${rowNumber}`, `Operation ${rowNumber} description`, 'text', operation.operationName);
      addInputCell(row, `operationDepWcr${rowNumber}`, `Operation ${rowNumber} department or WCR`, 'text', operation.workcenterDepartment);
      addInputCell(row, `operationAccQty${rowNumber}`, `Operation ${rowNumber} accepted quantity`, 'number');
      addInputCell(row, `operationRejQty${rowNumber}`, `Operation ${rowNumber} rejected quantity`, 'number');
      addInputCell(row, `operationStamp${rowNumber}`, `Operation ${rowNumber} stamp`);
      addInputCell(row, `operationSign${rowNumber}`, `Operation ${rowNumber} signature`);
      addDeleteButton(row, `Delete row ${rowNumber}`);
      tbody.append(row);
    });
  }

  const fieldAliases = {
    PART_NO: 'partNo', PARTNO: 'partNo', ITEM_NO: 'partNo', MATERIAL_NO: 'partNo',
    PART_NAME: 'partName', PARTNAME: 'partName', ITEM_NAME: 'partName', ITEM_DESC: 'partName', DESCRIPTION: 'partName', PART_DESCRIPTION: 'partName',
    PSL_NO: 'pslNo', PSLNO: 'pslNo', PSL: 'pslNo',
    ORDER_NO: 'orderNo', SHOP_ORDER_NO: 'orderNo', ORDER_NUMBER: 'orderNo', ORDERNUMBER: 'orderNo',
    WORK_ORDER_NO: 'workOrderNo', WORKORDER_NO: 'workOrderNo', WORK_ORDER: 'workOrderNo', WORKORDER: 'workOrderNo',
    SPLIT_QTY: 'jcQty', SPLIT_QUANTITY: 'jcQty', JC_QTY: 'jcQty', JOB_QTY: 'jcQty',
    QTY: 'qty', QUANTITY: 'qty', ORDER_QTY: 'qty', SHOP_ORDER_QTY: 'qty',
    HAL_DRG_NO: 'halDrawingNo', HAL_DRAWING_NO: 'halDrawingNo', ISSUE_NO: 'issueNo',
    MATERIAL_SPECIFICATION: 'materialSpecification', MATERIAL_ISSUE: 'materialIssue', MATERIAL_DESCRIPTION: 'materialDescription',
    MR_NO: 'mrNo', MATERIAL_TYPE: 'materialType', RECEIVED_CONDITION: 'receivedCondition',
    RAW_MATERIAL_DIA: 'rawMaterialDia', RAW_MATERIAL_LENGTH: 'rawMaterialLength', RAW_MATERIAL_BATCH_NO: 'rawMaterialBatchNo',
    ALTERNATIVE_MATERIAL_SIZE: 'alternativeMaterialSize', RUBBER_SPECIFICATION: 'rubberSpecification', RUBBER_QTY: 'rubberQty', RUBBER_SUPPLIER: 'rubberSupplier',
    TA_PC_REF: 'taPcRef', BS_WEIGHT: 'bsWeight', FORGING_WEIGHT: 'forgingWeight', FORGING_PLANT: 'forgingPlant', FORGING_BATCH_NO: 'forgingBatchNo', FORGING_QTY: 'forgingQty',
    RUBBER_BATCH_NO: 'rubberBatchNo', RUBBER_LTR_NO: 'rubberLtrNo', UN_FETT: 'unFett', FETT_WEIGHT: 'fettWeight', CASTING_BATCH_NO: 'castingBatchNo', CASTING_QTY: 'castingQty',
    PAD_WEIGHT: 'padWeight', PLATING_BATCH_NO: 'platingBatchNo', MIX_BATCH_NO: 'mixBatchNo', POWDER_QTY: 'powderQty'
  };

  function setMatchedFields(fields) {
    Object.entries(fields || {}).forEach(([name, value]) => {
      const target = fieldAliases[name.toUpperCase()] || name;
      setField(target, value);
    });
  }

  async function searchOrder() {
    const orderNo = document.querySelector('[name="orderNo"]')?.value.trim();
    const status = document.querySelector('#order-search-status');
    const button = document.querySelector('#search-order-button');
    if (!orderNo) {
      if (status) status.textContent = 'Enter an order number.';
      return;
    }

    if (button) button.disabled = true;
    if (status) status.textContent = 'Searching...';
    try {
      const response = await fetch(`/api/route-cards/lookup?orderNo=${encodeURIComponent(orderNo)}`);
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || 'No matching data was found.');

      setField('orderNo', data.orderNo || orderNo);
      setField('workOrderNo', data.workOrderNo);
      setField('partNo', data.partNo);
      setField('partName', data.partName);
      setField('pslNo', data.pslNo);
      setField('qty', data.qty);
      setField('jcQty', data.jcQty ?? data.qty);
      setField('traceWorkOrderNo', data.workOrderNo);
      setField('traceJcQty', data.jcQty ?? data.qty);
      setField('operationPslNo', data.pslNo);
      setField('operationWorkOrderNo', data.workOrderNo);
      setField('operationJcQty', data.jcQty ?? data.qty);
      setMatchedFields(data.matchedFields);
      populateOperations(data.operations);
      if (status) status.textContent = `Loaded data from ${data.sourceTable || 'Opcenter'}.`;
    } catch (error) {
      if (status) status.textContent = error.message;
    } finally {
      if (button) button.disabled = false;
    }
  }

  async function searchPart() {
    const partNo = document.querySelector('[name="partNo"]')?.value.trim();
    const status = document.querySelector('#part-search-status');
    const button = document.querySelector('#search-part-button');
    if (!partNo) {
      if (status) status.textContent = 'Enter a part number.';
      return;
    }

    if (button) button.disabled = true;
    if (status) status.textContent = 'Searching...';
    try {
      const response = await fetch(`/api/route-cards/lookup?partNo=${encodeURIComponent(partNo)}`);
      const data = await response.json();
      if (!response.ok) throw new Error(data.error || 'No matching data was found.');

      setField('partNo', data.partNo || partNo);
      setField('partName', data.partName);
      setField('pslNo', data.pslNo);
      setField('orderNo', data.orderNo);
      setField('workOrderNo', data.workOrderNo);
      setField('qty', data.qty);
      setField('jcQty', data.jcQty ?? data.qty);
      setField('traceWorkOrderNo', data.workOrderNo);
      setField('traceJcQty', data.jcQty ?? data.qty);
      setField('operationPslNo', data.pslNo);
      setField('operationWorkOrderNo', data.workOrderNo);
      setField('operationJcQty', data.jcQty ?? data.qty);
      setMatchedFields(data.matchedFields);
      populateOperations(data.operations);
      if (status) status.textContent = `Loaded ${data.records?.length || 0} matching rows and ${data.operations?.length || 0} operations from ${(data.sourceDatabases || []).filter((value, index, values) => values.indexOf(value) === index).join(', ') || 'HALFNF/HALNEW'}.`;
    } catch (error) {
      if (status) status.textContent = error.message;
    } finally {
      if (button) button.disabled = false;
    }
  }

  document.querySelector('#search-order-button')?.addEventListener('click', searchOrder);
  document.querySelector('[name="orderNo"]')?.addEventListener('keydown', event => {
    if (event.key === 'Enter') {
      event.preventDefault();
      searchOrder();
    }
  });
  document.querySelector('#search-part-button')?.addEventListener('click', searchPart);
  document.querySelector('[name="partNo"]')?.addEventListener('keydown', event => {
    if (event.key === 'Enter') {
      event.preventDefault();
      searchPart();
    }
  });
});
