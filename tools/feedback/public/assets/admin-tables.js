(() => {
  'use strict';
  const compare = new Intl.Collator(document.documentElement.lang || undefined, { numeric: true, sensitivity: 'base' });
  const value = cell => {
    if (!cell) return '';
    if (cell.dataset.sortValue !== undefined) return cell.dataset.sortValue;
    return cell.textContent.replace(/\s+/g, ' ').trim();
  };
  document.querySelectorAll('table').forEach(table => {
    const header = table.tHead?.rows[0] || table.rows[0];
    if (!header) return;
    Array.from(header.cells).forEach((cell, column) => {
      if (cell.tagName !== 'TH' || cell.colSpan !== 1) return;
      const button = document.createElement('button');
      button.type = 'button';
      button.style.cssText = 'font:inherit;font-weight:inherit;color:inherit;background:none;border:0;padding:0;cursor:pointer;text-align:inherit';
      while (cell.firstChild) button.append(cell.firstChild);
      const arrow = document.createElement('span');
      arrow.setAttribute('aria-hidden', 'true');
      const initialOrder = cell.getAttribute('aria-sort') || 'none';
      arrow.textContent = initialOrder === 'descending' ? ' ▼' : initialOrder === 'ascending' ? ' ▲' : ' ↕';
      button.append(arrow);
      cell.append(button);
      cell.setAttribute('aria-sort', initialOrder);
      button.addEventListener('click', () => {
        const descending = cell.getAttribute('aria-sort') === 'ascending';
        Array.from(header.cells).forEach(other => {
          other.setAttribute('aria-sort', 'none');
          const marker = other.querySelector('[aria-hidden]');
          if (marker) marker.textContent = ' ↕';
        });
        cell.setAttribute('aria-sort', descending ? 'descending' : 'ascending');
        arrow.textContent = descending ? ' ▼' : ' ▲';
        Array.from(table.tBodies).forEach(body => {
          const rows = Array.from(body.rows).filter(row => row !== header);
          // Keep empty-state rows spanning the whole table in place.
          if (rows.some(row => row.cells.length !== header.cells.length)) return;
          rows.map((row, index) => ({ row, index })).sort((a, b) => {
            const result = compare.compare(value(a.row.cells[column]), value(b.row.cells[column]));
            return (descending ? -result : result) || a.index - b.index;
          }).forEach(item => body.append(item.row));
        });
      });
    });
  });
})();
