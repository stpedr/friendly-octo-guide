import React, {useCallback, useState} from 'react';

const SVG_NAMESPACE = 'http://www.w3.org/2000/svg';

export default function DiagramDownload({filename}) {
  const [message, setMessage] = useState('');

  const download = useCallback(
    (event) => {
      const panel = event.currentTarget.closest('.diagram-panel');
      const sourceSvg = panel?.querySelector('.docusaurus-mermaid-container svg');

      if (!sourceSvg) {
        setMessage('Diagrama indisponível');
        return;
      }

      const svg = sourceSvg.cloneNode(true);
      const viewBox = sourceSvg.viewBox?.baseVal;
      const width = Math.ceil(viewBox?.width || sourceSvg.getBoundingClientRect().width);
      const height = Math.ceil(viewBox?.height || sourceSvg.getBoundingClientRect().height);
      const background = document.createElementNS(SVG_NAMESPACE, 'rect');

      svg.setAttribute('xmlns', SVG_NAMESPACE);
      svg.setAttribute('width', String(width));
      svg.setAttribute('height', String(height));
      svg.removeAttribute('style');
      background.setAttribute('width', '100%');
      background.setAttribute('height', '100%');
      background.setAttribute('fill', '#ffffff');
      svg.insertBefore(background, svg.firstChild);

      const serialized = new XMLSerializer().serializeToString(svg);
      const blob = new Blob([`<?xml version="1.0" encoding="UTF-8"?>\n${serialized}`], {
        type: 'image/svg+xml;charset=utf-8',
      });
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');

      anchor.href = url;
      anchor.download = `${filename}.svg`;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      setMessage('SVG baixado');
    },
    [filename],
  );

  return (
    <div className="diagram-download">
      <button
        aria-label={`Baixar o diagrama ${filename} em SVG`}
        className="diagram-download-button"
        data-testid={`download-${filename}`}
        onClick={download}
        title="Baixar este diagrama como arquivo SVG"
        type="button">
        <span aria-hidden="true" className="diagram-download-icon">↓</span>
        Baixar SVG
      </button>
      <span aria-live="polite" className="diagram-download-status">{message}</span>
    </div>
  );
}
