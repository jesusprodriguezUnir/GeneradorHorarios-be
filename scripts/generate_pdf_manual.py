import sys
import os
from pathlib import Path
from markdown_it import MarkdownIt
from reportlab.lib.pagesizes import A4
from reportlab.lib import colors
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether, Preformatted
)
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.pdfgen import canvas

# Initialize MarkdownIt with tables enabled
md_parser = MarkdownIt("commonmark", {"breaks": True, "html": True}).enable("table")

class NumberedCanvas(canvas.Canvas):
    """
    Two-pass canvas to dynamically compute and draw running headers, 
    footers, and page numbers ('Page X of Y').
    """
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            self.draw_page_elements(num_pages)
            super().showPage()
        super().save()

    def draw_page_elements(self, page_count):
        if self._pageNumber == 1:
            # Skip header/footer on cover page
            return
        
        self.saveState()
        
        # Corporate Colors
        gray = colors.HexColor("#7F8C8D")
        light_gray = colors.HexColor("#BDC3C7")
        
        width, height = A4
        
        # Running Header
        self.setFont("Helvetica-Bold", 8)
        self.setFillColor(colors.HexColor("#1A3A5C"))
        self.drawString(54, height - 40, "LECTIVO — MANUAL DE USUARIO")
        self.setStrokeColor(light_gray)
        self.setLineWidth(0.5)
        self.line(54, height - 46, width - 54, height - 46)
        
        # Running Footer
        self.line(54, 50, width - 54, 50)
        self.setFont("Helvetica", 9)
        self.setFillColor(gray)
        self.drawString(54, 35, "Generación automática y gestión de horarios LOMLOE")
        
        page_text = f"Página {self._pageNumber} de {page_count}"
        self.drawRightString(width - 54, 35, page_text)
        
        self.restoreState()


def draw_cover_background(canvas_obj, doc):
    """
    Callback to draw the dark navy background on the cover page
    before the flowables are rendered on top of it.
    """
    canvas_obj.saveState()
    navy = colors.HexColor("#1A3A5C")
    gold = colors.HexColor("#F9C846")
    teal = colors.HexColor("#4ECDC4")
    width, height = A4
    
    # Background fill
    canvas_obj.setFillColor(navy)
    canvas_obj.rect(0, 0, width, height, fill=1, stroke=0)
    
    # Gold decorative top border
    canvas_obj.setFillColor(gold)
    canvas_obj.rect(0, height - 120, width, 12, fill=1, stroke=0)
    
    # Teal decorative line
    canvas_obj.setFillColor(teal)
    canvas_obj.rect(0, height - 130, width, 6, fill=1, stroke=0)
    
    canvas_obj.restoreState()


def to_reportlab_markup(html_str):
    """
    Translates standard inline HTML tags from markdown-it 
    into ReportLab's supported XML-like paragraph markup.
    """
    html_str = html_str.replace("<strong>", "<b>").replace("</strong>", "</b>")
    html_str = html_str.replace("<em>", "<i>").replace("</em>", "</i>")
    # Wrap inline code in Courier with a soft colored font
    html_str = html_str.replace("<code>", '<font name="Courier" size="9" color="#1A3A5C"><b>').replace("</code>", "</b></font>")
    return html_str


def parse_markdown_to_flowables(md_path, styles):
    content = Path(md_path).read_text(encoding="utf-8")
    tokens = md_parser.parse(content)
    
    story = []
    
    # Styles Reference
    body_style = styles['Body']
    code_block_style = styles['CodeBlock']
    table_header_style = styles['TableHeader']
    table_cell_style = styles['TableCell']
    
    # Parser State Variables
    list_stack = []
    list_item_has_prefix = False
    
    in_heading = False
    heading_style = None
    heading_text = ""
    
    in_blockquote = False
    blockquote_elements = []
    
    in_table = False
    table_data = []
    current_row = []
    current_cell_text = ""
    in_cell = False
    cell_is_header = False
    
    for token in tokens:
        # --- HEADINGS ---
        if token.type == 'heading_open':
            in_heading = True
            tag = token.tag  # 'h1', 'h2', 'h3'
            if tag == 'h1':
                heading_style = styles['H1']
            elif tag == 'h2':
                heading_style = styles['H2']
            else:
                heading_style = styles['H3']
            heading_text = ""
            continue
            
        if token.type == 'heading_close':
            p = Paragraph(heading_text, heading_style)
            story.append(p)
            in_heading = False
            continue
            
        # --- LISTS ---
        if token.type == 'bullet_list_open':
            list_stack.append({"type": "ul", "index": 0})
            continue
            
        if token.type == 'bullet_list_close':
            list_stack.pop()
            continue
            
        if token.type == 'ordered_list_open':
            list_stack.append({"type": "ol", "index": 0})
            continue
            
        if token.type == 'ordered_list_close':
            list_stack.pop()
            continue
            
        if token.type == 'list_item_open':
            if list_stack:
                list_stack[-1]["index"] += 1
                list_item_has_prefix = True
            continue
            
        # --- BLOCKQUOTES (ALERTS) ---
        if token.type == 'blockquote_open':
            in_blockquote = True
            blockquote_elements = []
            continue
            
        if token.type == 'blockquote_close':
            # Format blockquote as a Table (Note Box) with left border
            border_color = colors.HexColor("#1A3A5C") # Default: Navy
            bg_color = colors.HexColor("#F4F6F7")      # Light gray-blue
            
            # Detect alerts: [!NOTE], [!TIP], [!WARNING], [!CAUTION]
            if blockquote_elements and isinstance(blockquote_elements[0], Paragraph):
                text = blockquote_elements[0].text
                if "[!NOTE]" in text:
                    border_color = colors.HexColor("#1A3A5C")
                    bg_color = colors.HexColor("#EDF2F7")
                    new_text = text.replace("[!NOTE]", "").replace("<b></b>", "").strip()
                    blockquote_elements[0] = Paragraph(new_text, blockquote_elements[0].style)
                elif "[!TIP]" in text:
                    border_color = colors.HexColor("#4ECDC4") # Teal
                    bg_color = colors.HexColor("#E6FFFA")
                    new_text = text.replace("[!TIP]", "").replace("<b></b>", "").strip()
                    blockquote_elements[0] = Paragraph(new_text, blockquote_elements[0].style)
                elif "[!WARNING]" in text:
                    border_color = colors.HexColor("#F9C846") # Gold
                    bg_color = colors.HexColor("#FEFCBF")
                    new_text = text.replace("[!WARNING]", "").replace("<b></b>", "").strip()
                    blockquote_elements[0] = Paragraph(new_text, blockquote_elements[0].style)
                elif "[!CAUTION]" in text:
                    border_color = colors.HexColor("#E53E3E") # Red
                    bg_color = colors.HexColor("#FFF5F5")
                    new_text = text.replace("[!CAUTION]", "").replace("<b></b>", "").strip()
                    blockquote_elements[0] = Paragraph(new_text, blockquote_elements[0].style)
            
            bq_table = Table([[blockquote_elements]], colWidths=[481.6])
            bq_table.setStyle(TableStyle([
                ('BACKGROUND', (0, 0), (-1, -1), bg_color),
                ('LINELEFT', (0, 0), (0, -1), 4, border_color),
                ('TOPPADDING', (0, 0), (-1, -1), 8),
                ('BOTTOMPADDING', (0, 0), (-1, -1), 8),
                ('LEFTPADDING', (0, 0), (-1, -1), 12),
                ('RIGHTPADDING', (0, 0), (-1, -1), 12),
                ('ALIGN', (0, 0), (-1, -1), 'LEFT'),
                ('VALIGN', (0, 0), (-1, -1), 'TOP'),
            ]))
            
            story.append(bq_table)
            story.append(Spacer(1, 10))
            in_blockquote = False
            continue
            
        # --- TABLES ---
        if token.type == 'table_open':
            in_table = True
            table_data = []
            continue
            
        if token.type == 'table_close':
            in_table = False
            if table_data:
                num_cols = len(table_data[0])
                col_width = 481.6 / num_cols
                col_widths = [col_width] * num_cols
                
                t = Table(table_data, colWidths=col_widths, repeatRows=1)
                t_style = TableStyle([
                    ('BACKGROUND', (0, 0), (-1, 0), colors.HexColor("#1A3A5C")),
                    ('ALIGN', (0, 0), (-1, -1), 'LEFT'),
                    ('VALIGN', (0, 0), (-1, -1), 'TOP'),
                    ('BOTTOMPADDING', (0, 0), (-1, -1), 6),
                    ('TOPPADDING', (0, 0), (-1, -1), 6),
                    ('LEFTPADDING', (0, 0), (-1, -1), 8),
                    ('RIGHTPADDING', (0, 0), (-1, -1), 8),
                    ('GRID', (0, 0), (-1, -1), 0.5, colors.HexColor("#BDC3C7")),
                ])
                
                # Alternating rows background
                for i in range(1, len(table_data)):
                    bg = colors.HexColor("#FAFAFA") if i % 2 == 1 else colors.HexColor("#FFFFFF")
                    t_style.add('BACKGROUND', (0, i), (-1, i), bg)
                    
                t.setStyle(t_style)
                story.append(t)
                story.append(Spacer(1, 10))
            continue
            
        if token.type == 'tr_open':
            current_row = []
            continue
            
        if token.type == 'tr_close':
            table_data.append(current_row)
            continue
            
        if token.type in ('th_open', 'td_open'):
            in_cell = True
            cell_is_header = (token.type == 'th_open')
            current_cell_text = ""
            continue
            
        if token.type in ('th_close', 'td_close'):
            style = table_header_style if cell_is_header else table_cell_style
            current_row.append(Paragraph(current_cell_text, style))
            in_cell = False
            continue
            
        # --- CODE BLOCKS (FENCED) ---
        if token.type == 'fence':
            code_text = token.content.strip()
            pf = Preformatted(code_text, code_block_style)
            
            code_table = Table([[pf]], colWidths=[481.6])
            code_table.setStyle(TableStyle([
                ('BACKGROUND', (0, 0), (-1, -1), colors.HexColor("#F5F6F7")),
                ('BOX', (0, 0), (-1, -1), 0.5, colors.HexColor("#E2E8F0")),
                ('TOPPADDING', (0, 0), (-1, -1), 8),
                ('BOTTOMPADDING', (0, 0), (-1, -1), 8),
                ('LEFTPADDING', (0, 0), (-1, -1), 10),
                ('RIGHTPADDING', (0, 0), (-1, -1), 10),
            ]))
            story.append(code_table)
            story.append(Spacer(1, 10))
            continue
            
        # --- PAGE BREAKS (hr) ---
        if token.type == 'hr':
            story.append(PageBreak())
            continue
            
        # --- TEXT / INLINE RENDERING ---
        if token.type == 'inline':
            # Skip if heading or table cell, since we collect them on open/close
            if in_heading:
                heading_text += to_reportlab_markup(md_parser.renderInline(token.content))
                continue
            if in_cell:
                current_cell_text += to_reportlab_markup(md_parser.renderInline(token.content))
                continue
                
            # Render list items with correct prefix & indent
            if list_stack:
                prefix = ""
                if list_item_has_prefix:
                    current_list = list_stack[-1]
                    if current_list["type"] == "ol":
                        prefix = f"{current_list['index']}.  "
                    else:
                        bullets = ["&bull;  ", "&#9642;  ", "&#9670;  "]
                        bullet_idx = (len(list_stack) - 1) % len(bullets)
                        prefix = bullets[bullet_idx]
                    list_item_has_prefix = False
                
                depth = len(list_stack)
                indent = 15 * depth
                
                item_style = ParagraphStyle(
                    f'List_d{depth}',
                    parent=body_style,
                    leftIndent=indent,
                    firstLineIndent=-10,
                    spaceAfter=4
                )
                
                inline_text = to_reportlab_markup(md_parser.renderInline(token.content))
                full_text = f"{prefix}{inline_text}"
                
                p = Paragraph(full_text, item_style)
                if in_blockquote:
                    blockquote_elements.append(p)
                else:
                    story.append(p)
            else:
                # Standard paragraph
                inline_text = to_reportlab_markup(md_parser.renderInline(token.content))
                p = Paragraph(inline_text, body_style)
                if in_blockquote:
                    blockquote_elements.append(p)
                else:
                    story.append(p)
                    
    return story


def build_pdf_manual(input_md, output_pdf):
    # Initialize Document Templates
    # Printable area: 595.27 x 841.89 A4 points.
    # Left/Right Margin = 20mm (56.7 points)
    # Top Margin = 25mm (70.8 points)
    # Bottom Margin = 20mm (56.7 points)
    # Total Printable Width = 595.27 - 113.4 = 481.87 (rounded to 481.6)
    doc = SimpleDocTemplate(
        output_pdf,
        pagesize=A4,
        leftMargin=56.7,
        rightMargin=56.7,
        topMargin=70.8,
        bottomMargin=56.7
    )
    
    # Styles Definition
    base_styles = getSampleStyleSheet()
    styles = {}
    
    styles['CoverTitle'] = ParagraphStyle(
        'CoverTitle',
        parent=base_styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=28,
        leading=34,
        textColor=colors.white,
        alignment=1,
        spaceAfter=15
    )
    
    styles['CoverSubtitle'] = ParagraphStyle(
        'CoverSubtitle',
        parent=base_styles['Normal'],
        fontName='Helvetica',
        fontSize=13,
        leading=18,
        textColor=colors.HexColor("#F9C846"),
        alignment=1,
        spaceAfter=80
    )
    
    styles['CoverMeta'] = ParagraphStyle(
        'CoverMeta',
        parent=base_styles['Normal'],
        fontName='Helvetica',
        fontSize=10,
        leading=15,
        textColor=colors.HexColor("#E5E7EB"),
        alignment=1,
        spaceAfter=6
    )
    
    styles['H1'] = ParagraphStyle(
        'H1',
        parent=base_styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=20,
        leading=24,
        textColor=colors.HexColor("#1A3A5C"),
        spaceBefore=22,
        spaceAfter=12,
        keepWithNext=True
    )
    
    styles['H2'] = ParagraphStyle(
        'H2',
        parent=base_styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=14,
        leading=18,
        textColor=colors.HexColor("#0D7A87"),
        spaceBefore=16,
        spaceAfter=8,
        keepWithNext=True
    )
    
    styles['H3'] = ParagraphStyle(
        'H3',
        parent=base_styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=11,
        leading=15,
        textColor=colors.HexColor("#2C3E50"),
        spaceBefore=12,
        spaceAfter=6,
        keepWithNext=True
    )
    
    styles['Body'] = ParagraphStyle(
        'Body',
        parent=base_styles['Normal'],
        fontName='Helvetica',
        fontSize=10,
        leading=14.5,
        textColor=colors.HexColor("#2C3E50"),
        spaceAfter=8
    )
    
    styles['CodeBlock'] = ParagraphStyle(
        'CodeBlock',
        parent=base_styles['Normal'],
        fontName='Courier',
        fontSize=8,
        leading=10,
        textColor=colors.HexColor("#1E2937"),
    )
    
    styles['TableHeader'] = ParagraphStyle(
        'TableHeader',
        parent=base_styles['Normal'],
        fontName='Helvetica-Bold',
        fontSize=9,
        leading=11,
        textColor=colors.white,
    )
    
    styles['TableCell'] = ParagraphStyle(
        'TableCell',
        parent=base_styles['Normal'],
        fontName='Helvetica',
        fontSize=8.5,
        leading=11,
        textColor=colors.HexColor("#2C3E50"),
    )
    
    # Story flowables list
    story = []
    
    # --- COVER PAGE FLOWABLES ---
    story.append(Spacer(1, 100))
    story.append(Paragraph("LECTIVO", styles['CoverTitle']))
    story.append(Paragraph("Manual de Usuario de la Aplicación", ParagraphStyle(
        'CoverHeader', parent=styles['CoverTitle'], fontSize=22, leading=26, spaceAfter=8
    )))
    story.append(Paragraph("Generación automática y gestión de horarios bajo normativa LOMLOE", styles['CoverSubtitle']))
    
    story.append(Spacer(1, 180))
    story.append(Paragraph("<b>Versión:</b> 1.0 (Oficial)", styles['CoverMeta']))
    story.append(Paragraph("<b>Fecha:</b> Junio 2026", styles['CoverMeta']))
    story.append(Paragraph("<b>Autor:</b> Equipo de Desarrollo de Lectivo", styles['CoverMeta']))
    story.append(Paragraph("<b>Entorno:</b> Comunidad de Madrid", styles['CoverMeta']))
    story.append(PageBreak())
    
    # --- CONTENT FLOWABLES ---
    body_flowables = parse_markdown_to_flowables(input_md, styles)
    story.extend(body_flowables)
    
    # Build document with cover background callback and dynamic page numbering canvas
    doc.build(
        story,
        onFirstPage=draw_cover_background,
        canvasmaker=NumberedCanvas
    )


if __name__ == "__main__":
    # Paths configuration
    current_dir = Path(__file__).resolve().parent
    workspace_root = current_dir.parent
    
    input_file = workspace_root / "doc" / "manual_usuario.md"
    output_file = workspace_root / "doc" / "manual_usuario.pdf"
    
    print(f"Compiling {input_file} into {output_file}...")
    try:
        build_pdf_manual(str(input_file), str(output_file))
        print("PDF generated successfully!")
    except Exception as e:
        print(f"Error compiling PDF: {e}", file=sys.stderr)
        sys.exit(1)
