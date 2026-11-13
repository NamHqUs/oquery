<!--
Layout template. A renderer replaces the placeholders below:
  {{product}}   product name from agenda.yml
  {{phase}}     phase title of the current item
  {{title}}     title of the current item
  {{toc}}       navigation generated from agenda.yml, with ready items only
  {{content}}   body of the item's file, with its H1 removed
  {{previous}}  previous ready item title, or empty
  {{next}}      next ready item title, or empty
Edit this file to change the page frame; edit topic files to change content.
-->

# {{product}} · {{title}}

**{{phase}}**

{{content}}

---

Previous: {{previous}} · Next: {{next}}

<details>
<summary>Contents</summary>

{{toc}}

</details>
